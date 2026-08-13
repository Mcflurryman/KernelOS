using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KernelOS.Web.Conversations;

public sealed class ConversationApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const int PageSize = 50;

    public Task<ConversationApiResult<IReadOnlyList<ConversationSummaryDto>>> ListConversationsAsync(int offset = 0, CancellationToken cancellationToken = default) =>
        GetListAsync<ConversationSummaryDto>($"conversations?limit={PageSize}&offset={offset}", cancellationToken);

    public Task<ConversationApiResult<IReadOnlyList<ConversationSummaryDto>>> ListConversationsAsync(CancellationToken cancellationToken) =>
        ListConversationsAsync(0, cancellationToken);

    public Task<ConversationApiResult<ConversationSummaryDto>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        GetAsync<ConversationSummaryDto>($"conversations/{conversationId:D}", cancellationToken);

    public Task<ConversationApiResult<IReadOnlyList<ConversationMessageDto>>> GetMessagesAsync(Guid conversationId, long? beforeSequence = null, CancellationToken cancellationToken = default) =>
        GetListAsync<ConversationMessageDto>($"conversations/{conversationId:D}/messages?limit={PageSize}&offset=0" + (beforeSequence is null ? string.Empty : $"&beforeSequence={beforeSequence.Value}"), cancellationToken);

    public Task<ConversationApiResult<IReadOnlyList<ConversationMessageDto>>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken) =>
        GetMessagesAsync(conversationId, null, cancellationToken);

    public Task<ConversationApiResult<ConversationSummaryDto>> CreateConversationAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ConversationSummaryDto>(HttpMethod.Post, "conversations", null, cancellationToken);

    public Task<ConversationApiResult<object>> DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"conversations/{conversationId:D}", null, cancellationToken);

    public Task<ConversationApiResult<ConversationTurnDto>> SendTurnAsync(Guid conversationId, string message, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationTurnDto>(HttpMethod.Post, $"conversations/{conversationId:D}/messages", new SendTurnRequestDto(message), cancellationToken);

    private async Task<ConversationApiResult<T>> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            var status = MapStatus(response.StatusCode);
            if (status != ConversationApiStatus.Success)
            {
                return new(status);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value is null ? new(ConversationApiStatus.InvalidResponse) : new(ConversationApiStatus.Success, value);
        }
        catch (OperationCanceledException)
        {
            return new(ConversationApiStatus.Cancelled);
        }
        catch (HttpRequestException)
        {
            return new(ConversationApiStatus.Uncertain);
        }
        catch (JsonException)
        {
            return new(ConversationApiStatus.InvalidResponse);
        }
    }

    private async Task<ConversationApiResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var status = MapStatus(response.StatusCode);
            if (method == HttpMethod.Delete && status == ConversationApiStatus.Success) return new(status);
            if (response.Content.Headers.ContentLength == 0) return new(status);

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (value is null) return new(ConversationApiStatus.InvalidResponse);
            return new(MapTurnStatus(status, value), value);
        }
        catch (OperationCanceledException) { return new(ConversationApiStatus.Cancelled); }
        catch (HttpRequestException) { return new(ConversationApiStatus.Uncertain); }
        catch (JsonException) { return new(ConversationApiStatus.InvalidResponse); }
    }

    private async Task<ConversationApiResult<IReadOnlyList<T>>> GetListAsync<T>(string uri, CancellationToken cancellationToken)
    {
        var result = await GetAsync<List<T>>(uri, cancellationToken);
        return result.IsSuccess
            ? new(ConversationApiStatus.Success, result.Value!.AsReadOnly())
            : new(result.Status);
    }

    private static ConversationApiStatus MapStatus(HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and < 300
            ? ConversationApiStatus.Success
            : statusCode switch
            {
                HttpStatusCode.Conflict => ConversationApiStatus.ConfirmationRequired,
                HttpStatusCode.BadRequest => ConversationApiStatus.BadRequest,
                HttpStatusCode.NotFound => ConversationApiStatus.NotFound,
                (HttpStatusCode)499 => ConversationApiStatus.Cancelled,
                >= HttpStatusCode.InternalServerError => ConversationApiStatus.ServerError,
                _ => ConversationApiStatus.Uncertain
            };

    private static ConversationApiStatus MapTurnStatus<T>(ConversationApiStatus status, T value) =>
        value is ConversationTurnDto turn && status == ConversationApiStatus.Success && string.Equals(turn.TurnStatus, "PartialSuccess", StringComparison.Ordinal)
            ? ConversationApiStatus.PartialSuccess
            : status;
}
