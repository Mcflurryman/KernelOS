using System.Text;
using System.Text.Json;
using KernelOS.Core.Documents;

namespace KernelOS.Infrastructure.Documents.Readers;

public sealed class JsonDocumentReader : DocumentReaderBase, IDocumentReader
{
    public DocumentReaderDescriptor Descriptor { get; } = new("json", [DocumentFormat.Json], ["json"], ["application/json"]);
    public bool CanRead(DocumentReadRequest request) => request.Format == DocumentFormat.Json;
    public async Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(request.File.Path, cancellationToken);
            if (bytes.Length == 0) return new(DocumentReadStatus.InvalidDocument, Error: "JSON documents cannot be empty.");
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            var text = new UTF8Encoding(false, true).GetString(bytes);
            var (limited, status, warnings) = Limit(text, request.Options);
            if (limited is null) return new(status, Warnings: warnings, Error: "The document exceeds the configured text limit.");
            var sections = new List<DocumentSection>(); var order = 0; Collect(json.RootElement, "$", sections, ref order);
            return new(status, CreateDocument(request, DocumentFormat.Json, "application/json", bytes, limited, "utf-8", sections, [], warnings), warnings);
        }
        catch (JsonException) { return new(DocumentReadStatus.InvalidDocument, Error: "The JSON document is invalid."); }
        catch (DecoderFallbackException) { return new(DocumentReadStatus.InvalidDocument, Error: "JSON must use UTF-8."); }
    }

    private static void Collect(JsonElement element, string path, List<DocumentSection> sections, ref int order)
    {
        if (element.ValueKind is JsonValueKind.Object) foreach (var property in element.EnumerateObject()) Collect(property.Value, $"{path}.{property.Name}", sections, ref order);
        else if (element.ValueKind is JsonValueKind.Array) { var index = 0; foreach (var item in element.EnumerateArray()) Collect(item, $"{path}[{index++}]", sections, ref order); }
        else sections.Add(new(path, null, element.GetRawText(), DocumentSectionKind.JsonValue, new(JsonPath: path), order++));
    }
}
