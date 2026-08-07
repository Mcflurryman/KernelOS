using KernelOS.Core.Knowledge;

namespace KernelOS.Core.Memory;

public enum MemoryStatus { Success, AlreadyExists, NotFound, InvalidRequest, Cancelled, Failed }

public sealed record MemoryItem(Guid Id, Guid KnowledgeItemId, KnowledgeItemType Type, string Content, KnowledgeSource Source, KnowledgeMetadata Metadata, string ContentHash);
public sealed record MemoryVersion(int Number, DateTimeOffset UpdatedAt, string ContentHash);
public sealed record MemoryDocument(Guid Id, Guid KnowledgeDocumentId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, MemoryVersion Version, IReadOnlyList<MemoryItem> Items, KnowledgeMetadata Metadata, string ContentHash);
public sealed record MemoryWarning(string Code, string Message);
public sealed record MemoryOptionsSnapshot(int MaxDocuments, int MaxItemsPerDocument, int MaxQueryResults);
public sealed record MemoryStoreRequest(KnowledgeDocument? KnowledgeDocument);
public sealed record MemoryStoreResult(MemoryStatus Status, MemoryDocument? Document = null, IReadOnlyList<MemoryWarning>? Warnings = null, string? Error = null);
public sealed record MemoryUpdateRequest(string Id, IReadOnlyList<MemoryItem>? Items, KnowledgeMetadata? Metadata);
public sealed record MemoryUpdateResult(MemoryStatus Status, MemoryDocument? Document = null, IReadOnlyList<MemoryWarning>? Warnings = null, string? Error = null);
public sealed record MemoryDeleteRequest(string Id);
public sealed record MemoryDeleteResult(MemoryStatus Status, string? Error = null);
public sealed record MemoryGetResult(MemoryStatus Status, MemoryDocument? Document = null, string? Error = null);
public sealed record MemoryQuery(string? Id = null, Guid? KnowledgeDocumentId = null, KnowledgeItemType? ItemType = null, string? ExactContent = null, string? ContentHash = null, string? MetadataKey = null, string? MetadataValue = null, int Limit = 100, int Offset = 0);
public sealed record MemoryQueryResult(MemoryStatus Status, IReadOnlyList<MemoryDocument>? Documents = null, string? Error = null);
