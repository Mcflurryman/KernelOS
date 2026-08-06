using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class ChatEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient client;

    public ChatEndpointTests(TestApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostChatReturnsBadRequestWhenMessageIsEmpty()
    {
        using var response = await client.PostAsync("/chat", JsonContent.Create(new { message = " " }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChatReturnsBadRequestWhenJsonIsNotUtf8()
    {
        using var content = new ByteArrayContent([0x7B, 0x22, 0x6D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x22, 0x3A, 0x22, 0xF3, 0x22, 0x7D]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync("/chat", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChatReturnsSimulatedResponseWithModel()
    {
        using var response = await client.PostAsync("/chat", JsonContent.Create(new { message = "Hola Kai" }));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("qwen3:8b", content, StringComparison.Ordinal);
        Assert.Contains("Hola desde Kai", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOllamaHealthReturnsControlledUnavailableStatus()
    {
        using var factory = new TestApplicationFactory(isOllamaAvailable: false);
        using var response = await factory.CreateClient().GetAsync("/health/ollama");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("unavailable", content, StringComparison.Ordinal);
        Assert.Contains("qwen3:8b", content, StringComparison.Ordinal);
    }
}

public sealed class OllamaChatModelTests
{
    [Fact]
    public async Task SendAsyncIncludesSystemPromptAndOrderedHistory()
    {
        string? sentPayload = null;
        var handler = new DelegateHttpMessageHandler(async request =>
        {
            sentPayload = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, """
                { "model": "qwen3:8b", "message": { "role": "assistant", "content": "Hola" } }
                """);
        });
        var model = CreateModel(handler);
        var request = new ChatRequest(
            "Pregunta actual",
            history: [new ChatMessage("user", "Primero"), new ChatMessage("assistant", "Después")]);

        var response = await model.SendAsync(request);

        Assert.True(response.Success);
        using var document = JsonDocument.Parse(sentPayload!);
        var messages = document.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Collection(
            messages,
            message =>
            {
                Assert.Equal("system", message.GetProperty("role").GetString());
                Assert.Equal("Eres Kai, el asistente personal local de KernelOS. Responde en español de forma clara y útil.", message.GetProperty("content").GetString());
            },
            message => Assert.Equal("Primero", message.GetProperty("content").GetString()),
            message => Assert.Equal("Después", message.GetProperty("content").GetString()),
            message => Assert.Equal("Pregunta actual", message.GetProperty("content").GetString()));
        Assert.False(document.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task SendAsyncConvertsHttpErrorToControlledError()
    {
        var model = CreateModel(new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))));

        var response = await model.SendAsync(new ChatRequest("Hola"));

        Assert.False(response.Success);
        Assert.Equal("http_error", response.Error);
    }

    [Fact]
    public async Task SendAsyncConvertsConnectionFailureToServiceUnavailable()
    {
        var model = CreateModel(new DelegateHttpMessageHandler(_ => throw new HttpRequestException("Connection refused.")));

        var response = await model.SendAsync(new ChatRequest("Hola"));

        Assert.False(response.Success);
        Assert.Equal("service_unavailable", response.Error);
    }

    [Fact]
    public async Task SendAsyncConvertsTimeoutToControlledError()
    {
        var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var model = CreateModel(handler, timeout: TimeSpan.FromMilliseconds(50));

        var response = await model.SendAsync(new ChatRequest("Hola"));

        Assert.False(response.Success);
        Assert.Equal("timeout", response.Error);
    }

    private static OllamaChatModel CreateModel(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://ollama.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        var options = Options.Create(new OllamaOptions());

        return new OllamaChatModel(
            new FakeHttpClientFactory(client),
            options,
            NullLogger<OllamaChatModel>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool isOllamaAvailable;

    public TestApplicationFactory()
        : this(true)
    {
    }

    internal TestApplicationFactory(bool isOllamaAvailable)
    {
        this.isOllamaAvailable = isOllamaAvailable;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatModel>();
            services.RemoveAll<IOllamaHealthCheck>();
            services.AddSingleton<IChatModel>(new FakeChatModel());
            services.AddSingleton<IOllamaHealthCheck>(new FakeOllamaHealthCheck(isOllamaAvailable));
        });
    }
}

internal sealed class FakeChatModel : IChatModel
{
    public Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse("Hola desde Kai", "qwen3:8b", 1, true, null));
}

internal sealed class FakeOllamaHealthCheck(bool isAvailable) : IOllamaHealthCheck
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(isAvailable);
}

internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

    public DelegateHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : this((request, _) => handler(request))
    {
    }

    public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        this.handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        handler(request, cancellationToken);
}
