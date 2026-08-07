using System.Net;
using System.Text;
using System.Text.Json;
using KernelOS.Core.Embeddings;
using KernelOS.Infrastructure;
using KernelOS.Infrastructure.Embeddings;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class OllamaEmbeddingGeneratorTests
{
    [Fact]
    public async Task GeneratesSingleEmbeddingUsingApiEmbed()
    {
        var handler = new StubHandler(_ => Json("{\"model\":\"embeddinggemma\",\"embeddings\":[[1,2,3]]}"));
        var generator = Generator(handler, dimensions: 3);
        var input = new EmbeddingInput(Guid.NewGuid(), "  Café\r\nA  ");
        var result = await generator.GenerateAsync(input);
        var request = handler.Request!;
        var body = await request.Content!.ReadAsStringAsync();
        var payload = JsonDocument.Parse(body).RootElement;
        Assert.Equal(EmbeddingStatus.Success, result.Status); Assert.Equal("/api/embed", request.RequestUri!.AbsolutePath); Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("embeddinggemma", payload.GetProperty("model").GetString()); Assert.Equal("Café\nA", payload.GetProperty("input").GetString()); Assert.Equal(input.Id, result.Vector!.InputId); Assert.Equal(3, result.Vector.Dimensions);
    }

    [Fact]
    public async Task UsesRealBatchAndPreservesOrderAndIds()
    {
        var handler = new StubHandler(_ => Json("{\"embeddings\":[[1,2],[3,4]]}"));
        var generator = Generator(handler, dimensions: 2);
        var first = new EmbeddingInput(Guid.NewGuid(), "first"); var second = new EmbeddingInput(Guid.NewGuid(), "second");
        var result = await generator.GenerateBatchAsync(new([first, second]));
        var body = await handler.Request!.Content!.ReadAsStringAsync();
        Assert.Equal(EmbeddingStatus.Success, result.Status); Assert.Contains("[\"first\",\"second\"]", body); Assert.Equal(first.Id, result.Results![0].Vector!.InputId); Assert.Equal(second.Id, result.Results[1].Vector!.InputId);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)] [InlineData(HttpStatusCode.NotFound)] [InlineData(HttpStatusCode.InternalServerError)]
    public async Task HttpErrorsReturnSafeFailure(HttpStatusCode status)
    {
        var result = await Generator(new StubHandler(_ => new(status)), dimensions: 2).GenerateAsync(new(Guid.NewGuid(), "text"));
        Assert.Equal(EmbeddingStatus.Failed, result.Status); Assert.Equal("Embedding provider is unavailable.", result.Error);
    }

    [Theory]
    [InlineData("{}")] [InlineData("")] [InlineData("not-json")] [InlineData("{\"embeddings\":[]}")] [InlineData("{\"embeddings\":[[1]]}")] [InlineData("{\"embeddings\":[[1,2],[3,4]]}")]
    public async Task InvalidResponsesReturnFailure(string payload)
    {
        var result = await Generator(new StubHandler(_ => Json(payload)), dimensions: 2).GenerateAsync(new(Guid.NewGuid(), "text"));
        Assert.Equal(EmbeddingStatus.Failed, result.Status);
    }

    [Fact]
    public async Task UsesProvidedContentHashWithoutTreatingItAsInputId()
    {
        var generator = Generator(new StubHandler(_ => Json("{\"embeddings\":[[1,2]]}")), dimensions: 2);
        var input = new EmbeddingInput(Guid.NewGuid(), "text", "CUSTOM-HASH");
        var result = await generator.GenerateAsync(input);
        Assert.Equal(EmbeddingStatus.Success, result.Status); Assert.Equal(input.Id, result.Vector!.InputId); Assert.Equal("CUSTOM-HASH", result.Vector.ContentHash);
    }

    [Fact]
    public void ModelInfoUsesConfiguredLocalModelAndDimensions()
    {
        var generator = Generator(new StubHandler(_ => Json("{\"embeddings\":[[1,2,3]]}")), dimensions: 3);
        Assert.Equal("ollama", generator.ModelInfo.Provider); Assert.Equal("embeddinggemma", generator.ModelInfo.Model); Assert.Null(generator.ModelInfo.Version); Assert.Equal(3, generator.ModelInfo.Dimensions); Assert.True(generator.ModelInfo.SupportsBatching);
    }

    [Fact]
    public async Task RejectsInvalidInputsLimitsAndDuplicateBatchIdsWithoutHttp()
    {
        var handler = new StubHandler(_ => Json("{\"embeddings\":[[1,2]]}")); var generator = Generator(handler, maxInput: 4, maxBatch: 1, dimensions: 2);
        Assert.Equal(EmbeddingStatus.InvalidInput, (await generator.GenerateAsync(new(Guid.Empty, "text"))).Status);
        Assert.Equal(EmbeddingStatus.TooLarge, (await generator.GenerateAsync(new(Guid.NewGuid(), "12345"))).Status);
        var duplicate = new EmbeddingInput(Guid.NewGuid(), "one");
        Assert.Equal(EmbeddingStatus.InvalidInput, (await generator.GenerateBatchAsync(new([duplicate, duplicate]))).Status);
        Assert.Equal(EmbeddingStatus.InvalidInput, (await generator.GenerateBatchAsync(new([]))).Status);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task CancellationAndTimeoutAreControlled()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var generator = Generator(new StubHandler(_ => Json("{\"embeddings\":[[1,2]]}")), dimensions: 2);
        Assert.Equal(EmbeddingStatus.Cancelled, (await generator.GenerateAsync(new(Guid.NewGuid(), "text"), cancelled.Token)).Status);
        var timeout = Generator(new StubHandler(_ => throw new OperationCanceledException()), dimensions: 2);
        Assert.Equal(EmbeddingStatus.Failed, (await timeout.GenerateAsync(new(Guid.NewGuid(), "text"))).Status);
    }

    private static OllamaEmbeddingGenerator Generator(StubHandler handler, int dimensions, int maxInput = 100, int maxBatch = 4) => new(new TestFactory(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") }), Options.Create(new EmbeddingOptions { Provider = "ollama", BaseUrl = "http://localhost:11434", Model = "embeddinggemma", TimeoutSeconds = 60, ExpectedDimensions = dimensions, MaxInputCharacters = maxInput, MaxBatchSize = maxBatch }));
    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private sealed class TestFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Request = request; Count++; return Task.FromResult(respond(request)); }
    }
}
