using System.Text.Json;
using KernelOS.Core.Documents;
using KernelOS.Core.Filesystem;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Documents;

public sealed class DocumentReadService(IFilesystemCapability filesystem, IDocumentReaderRouter router, IOptions<DocumentReaderOptions> options) : IDocumentReadService
{
    public async Task<DocumentReadResult> ReadAsync(string path, DocumentFormat? format = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(DocumentReadStatus.Cancelled, Error: "Document reading was cancelled.");
        if (string.IsNullOrWhiteSpace(path)) return new(DocumentReadStatus.InvalidDocument, Error: "A document path is required.");
        var arguments = new Dictionary<string, JsonElement> { ["path"] = JsonSerializer.SerializeToElement(path) };
        var resolved = await filesystem.ExecuteAsync("resolve", arguments, cancellationToken);
        if (!resolved.Success) return MapFilesystem(resolved.Error);
        var absolutePath = JsonSerializer.SerializeToElement(resolved.Data).GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(absolutePath)) return new(DocumentReadStatus.Failed, Error: "The document could not be resolved.");
        var metadata = await filesystem.ExecuteAsync("metadata", arguments, cancellationToken);
        if (!metadata.Success) return MapFilesystem(metadata.Error);
        var metadataElement = JsonSerializer.SerializeToElement(metadata.Data);
        if (metadataElement.GetProperty("Type").GetString() != "file") return new(DocumentReadStatus.InvalidDocument, Error: "The requested path is not a file.");
        var size = metadataElement.GetProperty("Size").GetInt64(); var current = options.Value;
        if (size > current.MaxFileSizeBytes) return new(DocumentReadStatus.TooLarge, Error: "The document exceeds the configured file limit.");
        var snapshot = new DocumentReaderOptionsSnapshot(current.MaxFileSizeBytes, current.MaxExtractedCharacters, current.MaxRows, current.MaxColumns, current.TimeoutSeconds, current.AllowPartialResults);
        return await router.ReadAsync(new(new FileReference(absolutePath), snapshot, format, DisplayReference: Path.GetFileName(absolutePath), SafeLogReference: Path.GetFileName(absolutePath)), cancellationToken);
    }

    private static DocumentReadResult MapFilesystem(string? error) => error switch
    { "unauthorized" => new(DocumentReadStatus.Unauthorized, Error: "The path is not authorized."), "not_found" => new(DocumentReadStatus.NotFound, Error: "The document was not found."), "cancelled" => new(DocumentReadStatus.Cancelled, Error: "Document reading was cancelled."), _ => new(DocumentReadStatus.Failed, Error: "The filesystem request failed.") };
}
