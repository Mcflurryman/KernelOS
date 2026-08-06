using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Documents;

namespace KernelOS.Tools;

public sealed class DocumentTool(IDocumentReadService? service = null) : IKernelTool
{
    public string Name => "document"; public string Description => "Reads supported documents."; public string Category => "documents";
    public IReadOnlyCollection<ToolCapability> Capabilities => [new("Read", "")];
    public IReadOnlyCollection<ToolParameter> Parameters => [new("operation", "Read operation.", "string", true), new("path", "Authorized document path.", "string", true), new("format", "Optional format.", "string", false)];
    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Arguments.TryGetValue("operation", out var operation) || operation.GetString() != "read" || !request.Arguments.TryGetValue("path", out var path) || path.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(path.GetString())) return ToolExecutionResult.InvalidArguments("operation 'read' and path are required.");
        if (service is null) return ToolExecutionResult.Failure("Document reading is unavailable.");
        var format = request.Arguments.TryGetValue("format", out var value) && value.ValueKind == JsonValueKind.String && Enum.TryParse<DocumentFormat>(value.GetString(), true, out var parsed) ? parsed : (DocumentFormat?)null;
        var result = await service.ReadAsync(path.GetString()!, format, cancellationToken);
        return result.Status switch
        {
            DocumentReadStatus.Success or DocumentReadStatus.PartialSuccess => ToolExecutionResult.Success("Document read completed.", JsonSerializer.SerializeToElement(new { result.Status, document = result.Document is null ? null : new { result.Document.Format, result.Document.Title, result.Document.TextContent, result.Document.Sections, result.Document.Tables, result.Document.Warnings, result.Document.ContentHash }, result.Warnings })),
            DocumentReadStatus.UnsupportedFormat or DocumentReadStatus.InvalidDocument => ToolExecutionResult.InvalidArguments(result.Error ?? "Invalid document."),
            DocumentReadStatus.TooLarge => new(ToolExecutionStatus.TooLarge, result.Error ?? "Too large."),
            DocumentReadStatus.Unauthorized => new(ToolExecutionStatus.Unauthorized, result.Error ?? "Unauthorized."),
            DocumentReadStatus.NotFound => ToolExecutionResult.NotFound(result.Error ?? "Not found."),
            DocumentReadStatus.Cancelled => ToolExecutionResult.Cancelled(),
            DocumentReadStatus.Failed => ToolExecutionResult.Failure(result.Error ?? "Document reading failed."),
            _ => ToolExecutionResult.Failure("Document reading failed.")
        };
    }
}
