using KernelOS.Core.Embeddings;

namespace KernelOS.Core.VectorIndex;

public enum VectorIndexStatus { Success, AlreadyExists, NotFound, InvalidRequest, TooLarge, Cancelled, Failed }

public sealed record VectorIndexWarning(string Code, string Message);
public sealed record VectorFamilyKey(string Provider, string Model, string? Version, int Dimensions)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(Model) && Dimensions > 0;
    public static VectorFamilyKey From(VectorRecord record) => new(record.Provider, record.Embedding.Model, record.Embedding.ModelVersion, record.Embedding.Dimensions);
}
public sealed record VectorReference(Guid VectorRecordId, Guid? MemoryDocumentId, Guid? KnowledgeDocumentId, Guid? MemoryItemId, Guid? KnowledgeItemId, Guid InputId);
public sealed record VectorRecord(Guid Id, string Provider, EmbeddingVector Embedding, Guid? MemoryDocumentId, Guid? KnowledgeDocumentId, Guid? MemoryItemId, Guid? KnowledgeItemId, string ContentHash, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record VectorAddRequest(VectorRecord? Record);
public sealed record VectorUpdateRequest(Guid Id, string? Provider, EmbeddingVector? Embedding, Guid? MemoryDocumentId, Guid? KnowledgeDocumentId, Guid? MemoryItemId, Guid? KnowledgeItemId, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record VectorDeleteRequest(Guid Id);
public sealed record VectorReplaceRequest(VectorFamilyKey? Family, IReadOnlyList<VectorRecord>? Records);
public sealed record VectorFamilyPatchRequest(VectorFamilyKey? Family, IReadOnlyList<Guid>? DeleteIds, IReadOnlyList<VectorRecord>? Upserts);
public sealed record VectorAddResult(VectorIndexStatus Status, VectorRecord? Record = null, string? Error = null);
public sealed record VectorUpdateResult(VectorIndexStatus Status, VectorRecord? Record = null, string? Error = null);
public sealed record VectorDeleteResult(VectorIndexStatus Status, string? Error = null);
public sealed record VectorReplaceResult(VectorIndexStatus Status, long ReplacedCount = 0, string? Error = null);
public sealed record VectorPatchResult(VectorIndexStatus Status, long DeletedCount = 0, long UpsertedCount = 0, string? Error = null);
public sealed record VectorGetResult(VectorIndexStatus Status, VectorRecord? Record = null, string? Error = null);
public sealed record VectorQuery(Guid? Id = null, Guid? InputId = null, Guid? MemoryDocumentId = null, Guid? KnowledgeDocumentId = null, Guid? MemoryItemId = null, Guid? KnowledgeItemId = null, string? Provider = null, string? Model = null, string? Version = null, int? Dimensions = null, string? ContentHash = null, string? MetadataKey = null, string? MetadataValue = null, int Limit = 100, int Offset = 0);
public sealed record VectorQueryResult(VectorIndexStatus Status, IReadOnlyList<VectorRecord>? Records = null, string? Error = null);
public sealed record VectorIndexOptionsSnapshot(int MaxRecords, int MaxQueryResults, int MaxMetadataEntries);
