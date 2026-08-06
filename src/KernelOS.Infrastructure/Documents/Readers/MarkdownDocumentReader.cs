using KernelOS.Core.Documents;
using System.Text;

namespace KernelOS.Infrastructure.Documents.Readers;

public sealed class MarkdownDocumentReader : DocumentReaderBase, IDocumentReader
{
    public DocumentReaderDescriptor Descriptor { get; } = new("markdown", [DocumentFormat.Markdown], ["md", "markdown"], ["text/markdown"]);
    public bool CanRead(DocumentReadRequest request) => request.Format == DocumentFormat.Markdown;
    public async Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (bytes, text, encoding) = await ReadTextAsync(request.File.Path, cancellationToken);
            var (limited, status, warnings) = Limit(text, request.Options);
            if (limited is null) return new(status, Warnings: warnings, Error: "The document exceeds the configured text limit.");
            var sections = new List<DocumentSection>(); var code = false; var order = 0;
            foreach (var (line, index) in limited.Split('\n').Select((line, index) => (line, index)))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("```", StringComparison.Ordinal)) code = !code;
                var kind = code ? DocumentSectionKind.CodeBlock : trimmed.StartsWith('#') ? DocumentSectionKind.Heading : DocumentSectionKind.Paragraph;
                sections.Add(new($"line-{index + 1}", kind == DocumentSectionKind.Heading ? trimmed.TrimStart('#', ' ') : null, trimmed, kind, new(Line: index + 1), order++));
            }
            return new(status, CreateDocument(request, DocumentFormat.Markdown, "text/markdown", bytes, limited, encoding, sections, [], warnings), warnings);
        }
        catch (DecoderFallbackException) { return new(DocumentReadStatus.InvalidDocument, Error: "The markdown encoding is not supported."); }
    }
}
