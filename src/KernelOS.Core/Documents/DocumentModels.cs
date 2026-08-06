using KernelOS.Core.Filesystem;

namespace KernelOS.Core.Documents;

public enum DocumentFormat { Text, Markdown, Json, Csv, Unknown }
public enum DocumentReadStatus { Success, PartialSuccess, UnsupportedFormat, InvalidDocument, TooLarge, Unauthorized, NotFound, Cancelled, Failed }
public enum DocumentWarningSeverity { Information, Warning, Error }
public enum DocumentSectionKind { Paragraph, Heading, List, CodeBlock, JsonValue, TextBlock }

public sealed record DocumentSource(string InternalReference, string DisplayReference, string SafeLogReference);
public sealed record DocumentLocator(int? Line = null, int? Column = null, string? JsonPath = null, int? Row = null, string? Section = null, string? Description = null);
public sealed record DocumentSection(string Id, string? Title, string Content, DocumentSectionKind Kind, DocumentLocator? Locator, int Order);
public sealed record DocumentTableRow(int Index, IReadOnlyList<string> Cells);
public sealed record DocumentTable(string Id, string? Name, IReadOnlyList<string> Headers, IReadOnlyList<DocumentTableRow> Rows, DocumentLocator? Locator);
public sealed record DocumentMetadata(string FileName, string Extension, string MimeType, string? Encoding, long SizeBytes, IReadOnlyDictionary<string, string>? Properties = null);
public sealed record DocumentWarning(string Code, string Message, DocumentWarningSeverity Severity, DocumentLocator? Locator = null);
public sealed record RawDocument(Guid Id, DocumentSource Source, DocumentFormat Format, string MimeType, string? Title, string? TextContent, IReadOnlyList<DocumentSection> Sections, IReadOnlyList<DocumentTable> Tables, DocumentMetadata Metadata, IReadOnlyList<DocumentWarning> Warnings, DateTimeOffset ReadAt, string ContentHash);
public sealed record DocumentReaderOptionsSnapshot(long MaxFileSizeBytes, int MaxExtractedCharacters, int MaxRows, int MaxColumns, int TimeoutSeconds, bool AllowPartialResults);
public sealed record DocumentReadRequest(FileReference File, DocumentReaderOptionsSnapshot Options, DocumentFormat? Format = null, string? MimeType = null, string? DisplayReference = null, string? SafeLogReference = null);
public sealed record DocumentReadResult(DocumentReadStatus Status, RawDocument? Document = null, IReadOnlyList<DocumentWarning>? Warnings = null, string? Error = null);
public sealed record DocumentReaderDescriptor(string Name, IReadOnlyCollection<DocumentFormat> Formats, IReadOnlyCollection<string> Extensions, IReadOnlyCollection<string> MimeTypes);
