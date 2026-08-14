using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace KernelOS.Web.Execution;
public enum ExecutionApiStatus { Success, NotFound, Conflict, BadRequest, Cancelled, ServerError, NetworkUncertain, InvalidPayload }
public sealed record ExecutionApiResult<T>(ExecutionApiStatus Status, T? Value = default) { public bool IsSuccess => Status == ExecutionApiStatus.Success; }
public sealed record ExecutionConfirmationDto(string? Status, bool Transitioned);
public sealed record ExecutionResultDto(string? Status, int CompletedTaskCount, int TotalTaskCount, string? ErrorCode);
public sealed class ExecutionApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public Task<ExecutionApiResult<ExecutionConfirmationDto>> ApproveAsync(Guid id, CancellationToken token = default) => SendAsync<ExecutionConfirmationDto>($"execution/confirmations/{id:D}", new { decision = "approve" }, token);
    public Task<ExecutionApiResult<ExecutionConfirmationDto>> RejectAsync(Guid id, CancellationToken token = default) => SendAsync<ExecutionConfirmationDto>($"execution/confirmations/{id:D}", new { decision = "reject" }, token);
    public Task<ExecutionApiResult<ExecutionResultDto>> ExecuteAsync(Guid id, CancellationToken token = default) => SendAsync<ExecutionResultDto>($"execution/pending/{id:D}/execute", null, token);
    private async Task<ExecutionApiResult<T>> SendAsync<T>(string uri, object? body, CancellationToken token) { try { using var request = new HttpRequestMessage(HttpMethod.Post, uri); if (body is not null) request.Content = JsonContent.Create(body, options: Options); using var response = await httpClient.SendAsync(request, token); var status = Map(response.StatusCode); var value = await response.Content.ReadFromJsonAsync<T>(Options, token); return value is null ? new(ExecutionApiStatus.InvalidPayload) : new(status, value); } catch (OperationCanceledException) { return new(ExecutionApiStatus.Cancelled); } catch (HttpRequestException) { return new(ExecutionApiStatus.NetworkUncertain); } catch (JsonException) { return new(ExecutionApiStatus.InvalidPayload); } }
    private static ExecutionApiStatus Map(HttpStatusCode code) => (int)code is >= 200 and < 300 ? ExecutionApiStatus.Success : code switch { HttpStatusCode.NotFound => ExecutionApiStatus.NotFound, HttpStatusCode.Conflict => ExecutionApiStatus.Conflict, HttpStatusCode.BadRequest => ExecutionApiStatus.BadRequest, (HttpStatusCode)499 => ExecutionApiStatus.Cancelled, >= HttpStatusCode.InternalServerError => ExecutionApiStatus.ServerError, _ => ExecutionApiStatus.NetworkUncertain };
}
