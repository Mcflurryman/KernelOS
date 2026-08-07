using System.Collections.Concurrent;
using KernelOS.Core.VectorIndex;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.VectorIndex;

public sealed class InMemoryVectorIndex : IVectorIndex, IDisposable
{
    private readonly ConcurrentDictionary<Guid, VectorRecord> records = new();
    private readonly ConcurrentDictionary<string, Guid> identities = new(StringComparer.Ordinal);
    private readonly VectorIndexOptionsSnapshot options;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public InMemoryVectorIndex(IOptions<VectorIndexOptions> options)
    {
        var value = options.Value;
        this.options = new(value.MaxRecords, value.MaxQueryResults, value.MaxMetadataEntries);
    }

    public async Task<VectorAddResult> AddAsync(VectorAddRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(VectorIndexStatus.Cancelled);
        if (!TryPrepare(request.Record, out var record, out var error)) return new(VectorIndexStatus.InvalidRequest, Error: error);
        try { await writeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        try
        {
            if (records.Count >= options.MaxRecords) return new(VectorIndexStatus.TooLarge, Error: "The vector record limit was reached.");
            var identity = Identity(record);
            if (records.ContainsKey(record.Id) || identities.ContainsKey(identity)) return new(VectorIndexStatus.AlreadyExists);
            if (!identities.TryAdd(identity, record.Id) || !records.TryAdd(record.Id, record)) { identities.TryRemove(identity, out _); return new(VectorIndexStatus.AlreadyExists); }
            return new(VectorIndexStatus.Success, Copy(record));
        }
        finally { writeGate.Release(); }
    }

    public async Task<VectorUpdateResult> UpdateAsync(VectorUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(VectorIndexStatus.Cancelled);
        if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Provider) || request.Embedding is null || !request.Embedding.IsValid() || !ValidMetadata(request.Metadata)) return new(VectorIndexStatus.InvalidRequest, Error: "The vector update request is invalid.");
        try { await writeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        try
        {
            if (!records.TryGetValue(request.Id, out var current)) return new(VectorIndexStatus.NotFound);
            var updated = new VectorRecord(request.Id, request.Provider, request.Embedding, request.MemoryDocumentId, request.KnowledgeDocumentId, request.MemoryItemId, request.KnowledgeItemId, request.Embedding.ContentHash, current.CreatedAt, DateTimeOffset.UtcNow, CopyMetadata(request.Metadata));
            var oldIdentity = Identity(current); var newIdentity = Identity(updated);
            if (!string.Equals(oldIdentity, newIdentity, StringComparison.Ordinal) && identities.TryGetValue(newIdentity, out var owner) && owner != request.Id) return new(VectorIndexStatus.AlreadyExists);
            records[request.Id] = updated;
            if (!string.Equals(oldIdentity, newIdentity, StringComparison.Ordinal)) { identities.TryRemove(oldIdentity, out _); identities[newIdentity] = request.Id; }
            return new(VectorIndexStatus.Success, Copy(updated));
        }
        finally { writeGate.Release(); }
    }

    public async Task<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(VectorIndexStatus.Cancelled);
        if (request.Id == Guid.Empty) return new(VectorIndexStatus.InvalidRequest);
        try { await writeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        try { if (!records.TryRemove(request.Id, out var record)) return new(VectorIndexStatus.NotFound); identities.TryRemove(Identity(record), out _); return new(VectorIndexStatus.Success); }
        finally { writeGate.Release(); }
    }

    public Task<VectorGetResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new VectorGetResult(VectorIndexStatus.Cancelled));
        return records.TryGetValue(id, out var record) ? Task.FromResult(new VectorGetResult(VectorIndexStatus.Success, Copy(record))) : Task.FromResult(new VectorGetResult(VectorIndexStatus.NotFound));
    }
    public Task<bool> ContainsAsync(Guid id, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(records.ContainsKey(id)); }
    public Task<long> CountAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult((long)records.Count); }

    public Task<VectorQueryResult> QueryAsync(VectorQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Cancelled));
        if (query.Limit <= 0 || query.Offset < 0) return Task.FromResult(new VectorQueryResult(VectorIndexStatus.InvalidRequest, Error: "The vector query is invalid."));
        try
        {
            var matches = records.Values.Where(record => Matches(record, query, cancellationToken)).OrderByDescending(record => record.UpdatedAt).ThenBy(record => record.Id).Skip(query.Offset).Take(Math.Min(query.Limit, options.MaxQueryResults)).Select(Copy).ToArray();
            return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Success, matches));
        }
        catch (OperationCanceledException) { return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Cancelled)); }
        catch { return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Failed, Error: "Vector query failed.")); }
    }

    private bool TryPrepare(VectorRecord? candidate, out VectorRecord record, out string error)
    {
        record = default!; error = "The vector add request is invalid.";
        if (candidate is null || candidate.Id == Guid.Empty || string.IsNullOrWhiteSpace(candidate.Provider) || candidate.Embedding is null || !candidate.Embedding.IsValid() || !ValidMetadata(candidate.Metadata)) return false;
        if (!string.Equals(candidate.ContentHash, candidate.Embedding.ContentHash, StringComparison.Ordinal)) return false;
        var now = DateTimeOffset.UtcNow;
        record = new(candidate.Id, candidate.Provider, candidate.Embedding, candidate.MemoryDocumentId, candidate.KnowledgeDocumentId, candidate.MemoryItemId, candidate.KnowledgeItemId, candidate.Embedding.ContentHash, now, now, CopyMetadata(candidate.Metadata));
        return true;
    }
    private bool ValidMetadata(IReadOnlyDictionary<string, string>? metadata) => metadata is null || (metadata.Count <= options.MaxMetadataEntries && metadata.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null));
    private static Dictionary<string, string>? CopyMetadata(IReadOnlyDictionary<string, string>? metadata) => metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    private static VectorRecord Copy(VectorRecord record) => record with { Metadata = CopyMetadata(record.Metadata) };
    private static string Identity(VectorRecord record) => string.Join('|', record.Embedding.InputId, record.Provider, record.Embedding.Model, record.Embedding.ModelVersion ?? "<null>", record.Embedding.Dimensions);
    private static bool Matches(VectorRecord record, VectorQuery query, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var embedding = record.Embedding;
        return (!query.Id.HasValue || record.Id == query.Id) && (!query.InputId.HasValue || embedding.InputId == query.InputId) && (!query.MemoryDocumentId.HasValue || record.MemoryDocumentId == query.MemoryDocumentId) && (!query.KnowledgeDocumentId.HasValue || record.KnowledgeDocumentId == query.KnowledgeDocumentId) && (!query.MemoryItemId.HasValue || record.MemoryItemId == query.MemoryItemId) && (!query.KnowledgeItemId.HasValue || record.KnowledgeItemId == query.KnowledgeItemId) && (query.Provider is null || record.Provider == query.Provider) && (query.Model is null || embedding.Model == query.Model) && (query.Version is null || embedding.ModelVersion == query.Version) && (!query.Dimensions.HasValue || embedding.Dimensions == query.Dimensions) && (query.ContentHash is null || record.ContentHash == query.ContentHash) && (query.MetadataKey is null || record.Metadata?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);
    }
    public void Dispose() => writeGate.Dispose();
}
