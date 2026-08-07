using KernelOS.Core.Documents;

namespace KernelOS.Core.Knowledge;

public enum KnowledgeItemType { Text, Heading, List, Code, JsonValue, Table, Metadata }
public enum KnowledgeBuildStatus { Success, PartialSuccess, InvalidDocument, TooLarge, Cancelled, Failed }

public sealed record KnowledgeLocator(string? SectionId = null, int? Line = null, int? Column = null, int? Row = null, string? JsonPath = null, string? Description = null);
public sealed record KnowledgeSource(Guid DocumentId, string SafeReference, string DisplayReference, KnowledgeLocator? Locator = null);
public sealed record KnowledgeMetadata(string MimeType, string? Format = null, string? Language = null, IReadOnlyDictionary<string, string>? Properties = null);
public sealed record KnowledgeWarning(string Code, string Message, DocumentWarningSeverity Severity = DocumentWarningSeverity.Warning, KnowledgeLocator? Locator = null);
public sealed record KnowledgeItem(Guid Id, KnowledgeItemType Type, string Content, int Order, KnowledgeSource Source, KnowledgeMetadata Metadata, string ContentHash);
public sealed record KnowledgeDocument(Guid Id, Guid SourceDocumentId, string? Title, IReadOnlyList<KnowledgeItem> Items, KnowledgeMetadata Metadata, IReadOnlyList<KnowledgeWarning> Warnings, DateTimeOffset CreatedAt, string ContentHash, string? Version = null);
public sealed record KnowledgeOptionsSnapshot(int MaxItemCharacters, int ChunkOverlapCharacters, int MaxItemsPerDocument, bool IncludeMetadataItems);
public sealed record KnowledgeBuildRequest(RawDocument? RawDocument, KnowledgeOptionsSnapshot Options);
public sealed record KnowledgeBuildResult(KnowledgeBuildStatus Status, KnowledgeDocument? Document = null, IReadOnlyList<KnowledgeWarning>? Warnings = null, string? Error = null);
