using KernelOS.Core.Documents;

namespace KernelOS.Infrastructure.Documents;

public sealed class DocumentReaderRegistry : IDocumentReaderRegistry
{
    public DocumentReaderRegistry(IEnumerable<IDocumentReader> readers)
    {
        Readers = readers.ToArray();
        EnsureUnique(Readers.Select(reader => reader.Descriptor.Name), "reader name");
        EnsureUnique(Readers.SelectMany(reader => reader.Descriptor.Extensions).Select(NormalizeExtension), "extension");
        EnsureUnique(Readers.SelectMany(reader => reader.Descriptor.Formats).Select(format => format.ToString()), "format");
        EnsureUnique(Readers.SelectMany(reader => reader.Descriptor.MimeTypes), "MIME type");
    }

    public IReadOnlyCollection<IDocumentReader> Readers { get; }
    public IDocumentReader? FindByFormat(DocumentFormat format) => Readers.SingleOrDefault(reader => reader.Descriptor.Formats.Contains(format));
    public IDocumentReader? FindByExtension(string extension) => Readers.SingleOrDefault(reader => reader.Descriptor.Extensions.Contains(NormalizeExtension(extension), StringComparer.OrdinalIgnoreCase));
    public IDocumentReader? FindByMimeType(string mimeType) => Readers.SingleOrDefault(reader => reader.Descriptor.MimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase));

    private static string NormalizeExtension(string extension) => extension.Trim().TrimStart('.');
    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var duplicate = values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate document reader {kind}: {duplicate.Key}.");
    }
}
