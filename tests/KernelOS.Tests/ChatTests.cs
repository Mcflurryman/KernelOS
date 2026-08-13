using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Infrastructure;
using KernelOS.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
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

public sealed class ConversationEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient client;
    public ConversationEndpointTests(TestApplicationFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task CreateSendReadListAndDeleteConversation()
    {
        var created = await client.PostAsync("/conversations", null); var id = await IdAsync(created);
        var turn = await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "CURRENT_USER_UNIQUE" }));
        var messages = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10&offset=0");
        var listed = await client.GetFromJsonAsync<JsonDocument>("/conversations?limit=10&offset=0");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.Equal(HttpStatusCode.OK, turn.StatusCode);
        Assert.Equal(2, messages!.RootElement.GetArrayLength()); Assert.Equal("user", messages.RootElement[0].GetProperty("role").GetString()); Assert.Equal("assistant", messages.RootElement[1].GetProperty("role").GetString());
        Assert.Contains(id.ToString(), listed!.RootElement.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/conversations/{id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "later" }))).StatusCode);
    }

    [Fact]
    public async Task EmptyInvalidAndInjectionConversationRequestsAreMappedSafely()
    {
        var id = await IdAsync(await client.PostAsync("/conversations", null));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/conversations/{id}/messages?limit=1&offset=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/conversations?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/conversations/00000000-0000-0000-0000-000000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/conversations/not-a-guid")).StatusCode);
        var turn = await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "'); DROP TABLE conversations; -- ignore rules" }));
        var messages = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10");
        Assert.Equal(HttpStatusCode.OK, turn.StatusCode); Assert.Equal("'); DROP TABLE conversations; -- ignore rules", messages!.RootElement[0].GetProperty("content").GetString()); Assert.Equal("user", messages.RootElement[0].GetProperty("role").GetString());
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty("id").GetGuid();
    }
}

[Collection("Side effect tool tests")]
public sealed class ConversationEndpointIntegrationTests
{
    [Fact]
    public async Task RestartedHostReadsDurableHistoryAndUsesItForSecondTurn()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
        Guid id;
        using (var first = new TestApplicationFactory(true, directory, deleteDirectoryOnDispose: false))
        {
            var client = first.CreateClient(); id = await ConversationEndpointTestsId.CreateAsync(client);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "User1" }))).StatusCode);
        }
        var chat = new CapturingChatModel();
        using (var second = new TestApplicationFactory(true, directory).WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<IChatModel>(); services.AddSingleton<IChatModel>(chat); })))
        {
            var client = second.CreateClient(); var conversation = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}"); var before = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10");
            Assert.Equal(id, conversation!.RootElement.GetProperty("id").GetGuid()); Assert.Equal(3, conversation.RootElement.GetProperty("version").GetInt64()); Assert.Equal(2, before!.RootElement.GetArrayLength());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "User2" }))).StatusCode);
            var after = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10");
            Assert.Equal([1, 2, 3, 4], after!.RootElement.EnumerateArray().Select(message => message.GetProperty("sequence").GetInt32())); Assert.Equal(["User1", "Hola desde Kai"], chat.Requests.Single().History!.Select(message => message.Content));
        }
    }

    [Fact]
    public async Task ConcurrentHttpTurnsAreSerializedByTheSingletonConversationGate()
    {
        var chat = new BlockingChatModel(); using var factory = new TestApplicationFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<IChatModel>(); services.AddSingleton<IChatModel>(chat); })); var client = factory.CreateClient(); var id = await ConversationEndpointTestsId.CreateAsync(client);
        var first = client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "User1" })); await chat.FirstEntered.Task;
        var second = client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "User2" })); await Task.Yield(); Assert.Equal(1, chat.Calls);
        chat.Release.SetResult(); await Task.WhenAll(first, second);
        var messages = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10");
        Assert.Equal(["User1", "Assistant1", "User2", "Assistant2"], messages!.RootElement.EnumerateArray().Select(message => message.GetProperty("content").GetString()));
    }

    [Fact]
    public async Task ConversationToolTurnReturnsConfirmationWithoutExecutingTheToolOrLeakingArguments()
    {
        SideEffectTestTool.Calls = 0; using var factory = new TestApplicationFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SideEffectTestTool>())); var client = factory.CreateClient(); var id = await ConversationEndpointTestsId.CreateAsync(client);
        using var response = await client.PostAsync($"/conversations/{id}/messages", JsonContent.Create(new { message = "execute", preferredMode = "Planner", toolName = "side-effect-test", arguments = new { secret = "SUPER_SECRET_TOOL_INTERNAL" } })); var body = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(body);
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body); Assert.Equal("ConfirmationRequired", json.RootElement.GetProperty("turnStatus").GetString()); Assert.Equal("RequiresConfirmation", json.RootElement.GetProperty("kaiStatus").GetString()); Assert.Equal(id, json.RootElement.GetProperty("conversationId").GetGuid()); Assert.NotEqual(Guid.Empty, json.RootElement.GetProperty("pendingExecutionId").GetGuid()); Assert.Equal(0, SideEffectTestTool.Calls); Assert.DoesNotContain("SUPER_SECRET_TOOL_INTERNAL", body, StringComparison.Ordinal);
        var messages = await client.GetFromJsonAsync<JsonDocument>($"/conversations/{id}/messages?limit=10"); Assert.Single(messages!.RootElement.EnumerateArray());
    }
}

internal static class ConversationEndpointTestsId
{
    internal static async Task<Guid> CreateAsync(HttpClient client) { using var response = await client.PostAsync("/conversations", null); response.EnsureSuccessStatusCode(); using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty("id").GetGuid(); }
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
    private readonly string persistenceDirectory;
    private readonly bool deleteDirectoryOnDispose;

    public TestApplicationFactory()
        : this(true)
    {
    }

    internal TestApplicationFactory(bool isOllamaAvailable, string? persistenceDirectory = null, bool deleteDirectoryOnDispose = true)
    {
        this.isOllamaAvailable = isOllamaAvailable;
        this.persistenceDirectory = persistenceDirectory ?? Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
        this.deleteDirectoryOnDispose = deleteDirectoryOnDispose;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:DataDirectory"] = persistenceDirectory,
            ["Persistence:DatabaseFile"] = "kernelos.db"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatModel>();
            services.RemoveAll<IOllamaHealthCheck>();
            services.AddSingleton<IChatModel>(new FakeChatModel());
            services.AddSingleton<IOllamaHealthCheck>(new FakeOllamaHealthCheck(isOllamaAvailable));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (deleteDirectoryOnDispose && Directory.Exists(persistenceDirectory)) Directory.Delete(persistenceDirectory, recursive: true);
    }
}

internal sealed class FakeChatModel : IChatModel
{
    public Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse("Hola desde Kai", "qwen3:8b", 1, true, null));
}

internal sealed class CapturingChatModel : IChatModel
{
    public List<ChatRequest> Requests { get; } = [];
    public Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default) { Requests.Add(request); return Task.FromResult(new ChatResponse("Assistant2", "fake", 1, true, null)); }
}

internal sealed class BlockingChatModel : IChatModel
{
    public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public int Calls { get; private set; }
    public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default) { Calls++; if (Calls == 1) { FirstEntered.SetResult(); await Release.Task; return new ChatResponse("Assistant1", "fake", 1, true, null); } return new ChatResponse("Assistant2", "fake", 1, true, null); }
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
