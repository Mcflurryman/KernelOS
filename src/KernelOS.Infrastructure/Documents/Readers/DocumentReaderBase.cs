using System.Security.Cryptography;
using System.Text;
using KernelOS.Core.Documents;

namespace KernelOS.Infrastructure.Documents.Readers;

public abstract class DocumentReaderBase
{
    protected static async Task<(byte[] Bytes, string Text, string Encoding)> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        await using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: false);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return (bytes, text, reader.CurrentEncoding.WebName);
    }

    protected static (string? Text, DocumentReadStatus Status, List<DocumentWarning> Warnings) Limit(string text, DocumentReaderOptionsSnapshot options)
    {
        if (text.Length <= options.MaxExtractedCharacters) return (text, DocumentReadStatus.Success, []);
        if (!options.AllowPartialResults) return (null, DocumentReadStatus.TooLarge, []);
        return (text[..options.MaxExtractedCharacters], DocumentReadStatus.PartialSuccess, [new("DOCUMENT_TRUNCATED", "Extracted text reached the configured limit.", DocumentWarningSeverity.Warning)]);
    }

    protected static RawDocument CreateDocument(DocumentReadRequest request, DocumentFormat format, string mimeType, byte[] bytes, string? text, string? encoding, IReadOnlyList<DocumentSection> sections, IReadOnlyList<DocumentTable> tables, IReadOnlyList<DocumentWarning> warnings)
    {
        var fileName = Path.GetFileName(request.File.Path);
        return new(Guid.NewGuid(), new(request.File.Path, request.DisplayReference ?? fileName, request.SafeLogReference ?? fileName), format, mimeType, fileName, text, sections, tables, new(fileName, Path.GetExtension(fileName), mimeType, encoding, bytes.LongLength), warnings, DateTimeOffset.UtcNow, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
