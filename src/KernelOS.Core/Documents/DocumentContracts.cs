namespace KernelOS.Core.Documents;

public interface IDocumentReader
{
    DocumentReaderDescriptor Descriptor { get; }
    bool CanRead(DocumentReadRequest request);
    Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentReaderRegistry
{
    IReadOnlyCollection<IDocumentReader> Readers { get; }
    IDocumentReader? FindByFormat(DocumentFormat format);
    IDocumentReader? FindByExtension(string extension);
    IDocumentReader? FindByMimeType(string mimeType);
}

public interface IDocumentReaderRouter
{
    Task<DocumentReadResult> ReadAsync(DocumentReadRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentReadService
{
    Task<DocumentReadResult> ReadAsync(string path, DocumentFormat? format = null, CancellationToken cancellationToken = default);
}
