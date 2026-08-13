using System.Net;
using System.Text;
using KernelOS.Web.Conversations;

namespace KernelOS.Tests;

public sealed class ConversationApiClientTests
{
    [Fact]
    public async Task ListConversationsAsyncReturnsSummariesFromSameOrigin()
    {
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            Assert.Equal("https://kernelos.test/conversations?limit=50&offset=0", request.RequestUri!.ToString());
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "[{\"id\":\"e4c2be4c-3dbf-4ad3-8732-f7d78bcbdd47\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-02T00:00:00Z\",\"version\":3}]"));
        });
        var client = CreateClient(handler);

        var result = await client.ListConversationsAsync();

        Assert.True(result.IsSuccess);
        var conversation = Assert.Single(result.Value!);
        Assert.Equal(3, conversation.Version);
    }

    [Fact]
    public async Task GetConversationAsyncReturnsConversation()
    {
        var id = Guid.NewGuid();
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, $"{{\"id\":\"{id:D}\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-02T00:00:00Z\",\"version\":1}}")));

        var result = await CreateClient(handler).GetConversationAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.Id);
    }

    [Fact]
    public async Task GetMessagesAsyncReturnsMessagesInApiOrder()
    {
        var conversationId = Guid.NewGuid();
        var firstMessageId = Guid.NewGuid();
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, $"[{{\"id\":\"{firstMessageId:D}\",\"conversationId\":\"{conversationId:D}\",\"sequence\":1,\"role\":\"user\",\"content\":\"one\\ntwo\",\"createdAt\":\"2026-01-01T00:00:00Z\"}}]")));

        var result = await CreateClient(handler).GetMessagesAsync(conversationId);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(result.Value!);
        Assert.Equal(1, message.Sequence);
        Assert.Equal("one\ntwo", message.Content);
    }

    [Fact]
    public async Task GetConversationAsyncMapsNotFoundWithoutExposingResponse()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await CreateClient(handler).GetConversationAsync(Guid.NewGuid());

        Assert.Equal(ConversationApiStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ListConversationsAsyncMapsServerErrorWithoutExposingResponse()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var result = await CreateClient(handler).ListConversationsAsync();

        Assert.Equal(ConversationApiStatus.ServerError, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task CreateConversationAsyncReturnsCreatedConversation()
    {
        var id = Guid.NewGuid();
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://kernelos.test/conversations", request.RequestUri!.ToString());
            return Task.FromResult(JsonResponse(HttpStatusCode.Created, $"{{\"id\":\"{id:D}\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"updatedAt\":\"2026-01-01T00:00:00Z\",\"version\":1}}"));
        });

        var result = await CreateClient(handler).CreateConversationAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.Id);
    }

    [Fact]
    public async Task DeleteConversationAsyncMapsNotFound()
    {
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var result = await CreateClient(handler).DeleteConversationAsync(Guid.NewGuid());

        Assert.Equal(ConversationApiStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SendTurnAsyncMapsSuccessAndPostsOnlyMessage()
    {
        var id = Guid.NewGuid();
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler(async request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("{\"message\":\"hello\"}", await request.Content!.ReadAsStringAsync());
            return JsonResponse(HttpStatusCode.OK, $"{{\"conversationId\":\"{id:D}\",\"userMessageId\":\"{Guid.NewGuid():D}\",\"assistantMessageId\":\"{Guid.NewGuid():D}\",\"turnStatus\":\"Success\",\"answer\":\"Hi\"}}");
        });

        var result = await CreateClient(handler).SendTurnAsync(id, "hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hi", result.Value!.Answer);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "PartialSuccess", ConversationApiStatus.PartialSuccess)]
    [InlineData(HttpStatusCode.Conflict, "ConfirmationRequired", ConversationApiStatus.ConfirmationRequired)]
    [InlineData((HttpStatusCode)499, "Cancelled", ConversationApiStatus.Cancelled)]
    public async Task SendTurnAsyncMapsTurnStatuses(HttpStatusCode statusCode, string turnStatus, ConversationApiStatus expected)
    {
        var id = Guid.NewGuid();
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(statusCode, $"{{\"conversationId\":\"{id:D}\",\"turnStatus\":\"{turnStatus}\",\"answer\":\"response\"}}")));

        var result = await CreateClient(handler).SendTurnAsync(id, "message");

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task SendTurnAsyncMapsServerErrorAndDoesNotRetry()
    {
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)); });

        var result = await CreateClient(handler).SendTurnAsync(Guid.NewGuid(), "message");

        Assert.Equal(ConversationApiStatus.ServerError, result.Status);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SendTurnAsyncMapsNetworkFailureAsUncertain()
    {
        using var handler = new DelegateHttpMessageHandler(_ => throw new HttpRequestException());

        var result = await CreateClient(handler).SendTurnAsync(Guid.NewGuid(), "message");

        Assert.Equal(ConversationApiStatus.Uncertain, result.Status);
    }

    [Fact]
    public async Task SendTurnAsyncMapsInvalidJson()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, "not-json")));

        var result = await CreateClient(handler).SendTurnAsync(Guid.NewGuid(), "message");

        Assert.Equal(ConversationApiStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task ListConversationsAsyncMapsInvalidJson()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, "not-json")));

        var result = await CreateClient(handler).ListConversationsAsync();

        Assert.Equal(ConversationApiStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task ListConversationsAsyncMapsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new DelegateHttpMessageHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        cancellation.Cancel();

        var result = await CreateClient(handler).ListConversationsAsync(cancellation.Token);

        Assert.Equal(ConversationApiStatus.Cancelled, result.Status);
    }

    private static ConversationApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://kernelos.test/") });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
