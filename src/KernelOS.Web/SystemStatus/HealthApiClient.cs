using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KernelOS.Web.SystemStatus;

public sealed class HealthApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<HealthCheckResult> GetKernelApiAsync(CancellationToken cancellationToken = default) =>
        GetAsync("health", cancellationToken);

    public Task<HealthCheckResult> GetOllamaAsync(CancellationToken cancellationToken = default) =>
        GetAsync("health/ollama", cancellationToken);

    private async Task<HealthCheckResult> GetAsync(string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable) return new(HealthStatus.Offline);
            if (!response.IsSuccessStatusCode) return new(HealthStatus.Degraded);
            var payload = await response.Content.ReadFromJsonAsync<HealthResponseDto>(JsonOptions, cancellationToken);
            return string.Equals(payload?.Status, "ok", StringComparison.OrdinalIgnoreCase)
                ? new(HealthStatus.Online)
                : new(HealthStatus.Degraded);
        }
        catch (OperationCanceledException) { return new(HealthStatus.Degraded); }
        catch (HttpRequestException) { return new(HealthStatus.Offline); }
        catch (JsonException) { return new(HealthStatus.Degraded); }
    }
}

public sealed record HealthResponseDto(string? Status);
public sealed record HealthCheckResult(HealthStatus Status);
public enum HealthStatus { Online, Degraded, Offline }
