using KernelOS.Core.Documents;
using System.Text;

namespace KernelOS.Infrastructure.Documents.Readers;

public sealed class TxtDocumentReader : DocumentReaderBase, IDocumentReader
{
    public DocumentReaderDescriptor Descriptor { get; } = new("txt", [DocumentFormat.Text], ["txt"], ["text/plain"]);
    public bool CanRead(DocumentReadRequest request) => request.Format == DocumentFormat.Text;
    public async Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (bytes, text, encoding) = await ReadTextAsync(request.File.Path, cancellationToken);
            var (limited, status, warnings) = Limit(text, request.Options);
            if (limited is null) return new(status, Warnings: warnings, Error: "The document exceeds the configured text limit.");
            var sections = limited.Split('\n').Select((line, index) => new DocumentSection($"line-{index + 1}", null, line.TrimEnd('\r'), DocumentSectionKind.TextBlock, new(Line: index + 1), index)).ToArray();
            return new(status, CreateDocument(request, DocumentFormat.Text, "text/plain", bytes, limited, encoding, sections, [], warnings), warnings);
        }
        catch (DecoderFallbackException) { return new(DocumentReadStatus.InvalidDocument, Error: "The text encoding is not supported."); }
    }
}
