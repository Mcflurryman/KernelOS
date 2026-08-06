using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure;

public sealed class OllamaHealthCheck(
    IHttpClientFactory httpClientFactory,
    ILogger<OllamaHealthCheck> logger) : IOllamaHealthCheck
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient(ServiceCollectionExtensions.OllamaHttpClientName)
                .GetAsync("api/tags", cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            OllamaHealthLog.CheckFailed(logger, exception);
            return false;
        }
    }
}

internal static partial class OllamaHealthLog
{
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Ollama availability check failed.")]
    public static partial void CheckFailed(ILogger logger, Exception exception);
}
