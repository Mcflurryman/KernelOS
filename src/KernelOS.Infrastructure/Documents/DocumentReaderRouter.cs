using System.Diagnostics;
using KernelOS.Core.Documents;
using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure.Documents;

public sealed class DocumentReaderRouter(IDocumentReaderRegistry registry, ILogger<DocumentReaderRouter> logger) : IDocumentReaderRouter
{
    public async Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(DocumentReadStatus.Cancelled, Error: "Document reading was cancelled.");
        var format = request.Format is { } explicitFormat and not DocumentFormat.Unknown
            ? explicitFormat
            : InferFormat(request.File.Path, request.MimeType);
        var reader = registry.FindByFormat(format);
        if (reader is null) return new(DocumentReadStatus.UnsupportedFormat, Error: "No compatible document reader is registered.");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await reader.ReadAsync(request with { Format = format }, cancellationToken);
            DocumentReaderLog.Completed(logger, reader.Descriptor.Name, result.Status, stopwatch.ElapsedMilliseconds, request.SafeLogReference ?? "document");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(DocumentReadStatus.Cancelled, Error: "Document reading was cancelled.");
        }
        catch (Exception exception)
        {
            DocumentReaderLog.Failed(logger, exception, reader.Descriptor.Name, request.SafeLogReference ?? "document");
            return new(DocumentReadStatus.Failed, Error: "The document could not be read.");
        }
    }

    private static DocumentFormat InferFormat(string path, string? mimeType) =>
        mimeType?.ToLowerInvariant() switch
        {
            "text/plain" => DocumentFormat.Text, "text/markdown" => DocumentFormat.Markdown,
            "application/json" => DocumentFormat.Json, "text/csv" => DocumentFormat.Csv,
            _ => Path.GetExtension(path).ToLowerInvariant() switch { ".txt" => DocumentFormat.Text, ".md" or ".markdown" => DocumentFormat.Markdown, ".json" => DocumentFormat.Json, ".csv" => DocumentFormat.Csv, _ => DocumentFormat.Unknown }
        };
}

internal static partial class DocumentReaderLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Document reader {Reader} completed with {Status} in {DurationMilliseconds} ms for {Reference}.")]
    public static partial void Completed(ILogger logger, string reader, DocumentReadStatus status, long durationMilliseconds, string reference);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Document reader {Reader} failed for {Reference}.")]
    public static partial void Failed(ILogger logger, Exception exception, string reader, string reference);
}
