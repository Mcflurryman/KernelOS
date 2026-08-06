using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure;

public sealed class OllamaChatModel(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    ILogger<OllamaChatModel> logger) : IChatModel
{
    public async Task<ChatResponse> SendAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var configuredOptions = options.Value;
        var messages = CreateMessages(request, configuredOptions.SystemPrompt);

        try
        {
            OllamaChatLog.RequestStarted(logger, "api/chat", configuredOptions.Model);
            using var response = await httpClientFactory.CreateClient(ServiceCollectionExtensions.OllamaHttpClientName)
                .PostAsJsonAsync(
                    "api/chat",
                    new OllamaApiRequest(configuredOptions.Model, messages),
                    cancellationToken);

            OllamaChatLog.ResponseReceived(logger, "api/chat", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                OllamaChatLog.HttpError(logger, (int)response.StatusCode);
                return Failure(configuredOptions.Model, stopwatch, "http_error");
            }

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaApiResponse>(cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(ollamaResponse?.Message?.Content))
            {
                OllamaChatLog.InvalidResponse(logger);
                return Failure(configuredOptions.Model, stopwatch, "invalid_response");
            }

            return new ChatResponse(
                ollamaResponse.Message.Content,
                string.IsNullOrWhiteSpace(ollamaResponse.Model) ? configuredOptions.Model : ollamaResponse.Model,
                stopwatch.ElapsedMilliseconds,
                true,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(configuredOptions.Model, stopwatch, "cancelled");
        }
        catch (OperationCanceledException exception)
        {
            OllamaChatLog.Timeout(logger, exception);
            return Failure(configuredOptions.Model, stopwatch, "timeout");
        }
        catch (HttpRequestException exception)
        {
            OllamaChatLog.Unavailable(logger, exception);
            return Failure(configuredOptions.Model, stopwatch, "service_unavailable");
        }
        catch (JsonException exception)
        {
            OllamaChatLog.InvalidResponse(logger, exception);
            return Failure(configuredOptions.Model, stopwatch, "invalid_response");
        }
    }

    private static List<OllamaApiMessage> CreateMessages(ChatRequest request, string configuredSystemPrompt)
    {
        var messages = new List<OllamaApiMessage>();
        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? configuredSystemPrompt
            : request.SystemPrompt;

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new OllamaApiMessage("system", systemPrompt));
        }

        if (request.History is not null)
        {
            messages.AddRange(request.History.Select(message => new OllamaApiMessage(message.Role, message.Content)));
        }

        messages.Add(new OllamaApiMessage("user", request.Message));
        return messages;
    }

    private static ChatResponse Failure(string model, Stopwatch stopwatch, string error) =>
        new(string.Empty, model, stopwatch.ElapsedMilliseconds, false, error);
}

internal static partial class OllamaChatLog
{
    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Sending a request to Ollama endpoint {Endpoint} with model {Model}.")]
    public static partial void RequestStarted(ILogger logger, string endpoint, string model);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Ollama returned HTTP status {StatusCode}.")]
    public static partial void HttpError(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Ollama endpoint {Endpoint} returned HTTP status {StatusCode}.")]
    public static partial void ResponseReceived(ILogger logger, string endpoint, int statusCode);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Ollama returned an invalid response.")]
    public static partial void InvalidResponse(ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Ollama returned an invalid response.")]
    public static partial void InvalidResponse(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "Ollama request timed out.")]
    public static partial void Timeout(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning, Message = "Ollama is unavailable.")]
    public static partial void Unavailable(ILogger logger, Exception exception);
}
