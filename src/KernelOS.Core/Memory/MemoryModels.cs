using KernelOS.Core.Knowledge;

namespace KernelOS.Core.Memory;

public enum MemoryStatus { Success, AlreadyExists, NotFound, InvalidRequest, Cancelled, Failed }
public enum MemoryMutationType { Created, Updated, Deleted }

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
public sealed record MemoryQuery(string? Id = null, Guid? KnowledgeDocumentId = null, Guid? MemoryItemId = null, KnowledgeItemType? ItemType = null, string? ExactContent = null, string? ContentHash = null, string? MetadataKey = null, string? MetadataValue = null, int Limit = 100, int Offset = 0);
public sealed record MemoryQueryResult(MemoryStatus Status, IReadOnlyList<MemoryDocument>? Documents = null, string? Error = null);
public sealed record MemorySnapshotResult(MemoryStatus Status, MemorySnapshot? Snapshot = null, string? Error = null);
public sealed record MemoryMutationCommitted(MemoryMutationType Type, MemoryDocument? Previous, MemoryDocument? Current, DateTimeOffset CommittedAt);

public sealed class MemorySnapshot
{
    public MemorySnapshot(IEnumerable<MemoryDocument> documents, DateTimeOffset capturedAt)
    {
        var copies = documents.Select(CopyDocument).ToArray();
        Documents = Array.AsReadOnly(copies);
        TotalDocuments = copies.Length;
        TotalItems = copies.Sum(document => document.Items.Count);
        CapturedAt = capturedAt;
    }

    public IReadOnlyList<MemoryDocument> Documents { get; }
    public int TotalDocuments { get; }
    public int TotalItems { get; }
    public DateTimeOffset CapturedAt { get; }

    private static MemoryDocument CopyDocument(MemoryDocument document) => document with
    {
        Version = document.Version with { },
        Items = Array.AsReadOnly(document.Items.Select(CopyItem).ToArray()),
        Metadata = CopyMetadata(document.Metadata)
    };

    private static MemoryItem CopyItem(MemoryItem item) => item with
    {
        Source = item.Source with { Locator = item.Source.Locator is null ? null : item.Source.Locator with { } },
        Metadata = CopyMetadata(item.Metadata)
    };

    private static KnowledgeMetadata CopyMetadata(KnowledgeMetadata metadata) => metadata with
    {
        Properties = metadata.Properties is null
            ? null
            : new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata.Properties, StringComparer.Ordinal))
    };
}
