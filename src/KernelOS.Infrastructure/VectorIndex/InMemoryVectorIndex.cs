using KernelOS.Core.VectorIndex;
using Microsoft.Extensions.Options;
using System.Text;

namespace KernelOS.Infrastructure.VectorIndex;

public sealed class InMemoryVectorIndex : IVectorIndex, IDisposable
{
    private readonly VectorIndexOptionsSnapshot options;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private IndexState state = IndexState.Empty;

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
            var current = Volatile.Read(ref state);
            if (current.Records.Count >= options.MaxRecords) return new(VectorIndexStatus.TooLarge, Error: "The vector record limit was reached.");
            var identity = Identity(record);
            if (current.Records.ContainsKey(record.Id) || current.Identities.ContainsKey(identity)) return new(VectorIndexStatus.AlreadyExists);
            var records = new Dictionary<Guid, VectorRecord>(current.Records) { [record.Id] = record };
            Volatile.Write(ref state, IndexState.Create(records));
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
            var current = Volatile.Read(ref state);
            if (!current.Records.TryGetValue(request.Id, out var existing)) return new(VectorIndexStatus.NotFound);
            var updated = new VectorRecord(request.Id, request.Provider, request.Embedding, request.MemoryDocumentId, request.KnowledgeDocumentId, request.MemoryItemId, request.KnowledgeItemId, request.Embedding.ContentHash, existing.CreatedAt, DateTimeOffset.UtcNow, CopyMetadata(request.Metadata));
            var oldIdentity = Identity(existing); var newIdentity = Identity(updated);
            if (!string.Equals(oldIdentity, newIdentity, StringComparison.Ordinal) && current.Identities.TryGetValue(newIdentity, out var owner) && owner != request.Id) return new(VectorIndexStatus.AlreadyExists);
            var records = new Dictionary<Guid, VectorRecord>(current.Records) { [request.Id] = updated };
            Volatile.Write(ref state, IndexState.Create(records));
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
        try
        {
            var current = Volatile.Read(ref state);
            if (!current.Records.ContainsKey(request.Id)) return new(VectorIndexStatus.NotFound);
            var records = new Dictionary<Guid, VectorRecord>(current.Records); records.Remove(request.Id);
            Volatile.Write(ref state, IndexState.Create(records));
            return new(VectorIndexStatus.Success);
        }
        finally { writeGate.Release(); }
    }

    public async Task<VectorReplaceResult> ReplaceFamilyAsync(VectorReplaceRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(VectorIndexStatus.Cancelled);
        if (!TryPrepareReplacement(request, out var family, out var replacements, out var error)) return new(VectorIndexStatus.InvalidRequest, Error: error);
        try { await writeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = Volatile.Read(ref state);
            var records = current.Records
                .Where(pair => !VectorFamilyKey.From(pair.Value).Equals(family))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var record in replacements) records.Add(record.Id, record);
            if (records.Count > options.MaxRecords) return new(VectorIndexStatus.TooLarge, Error: "The vector record limit was reached.");
            Volatile.Write(ref state, IndexState.Create(records));
            return new(VectorIndexStatus.Success, replacements.Count);
        }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        finally { writeGate.Release(); }
    }

    public async Task<VectorPatchResult> ApplyFamilyPatchAsync(VectorFamilyPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(VectorIndexStatus.Cancelled);
        if (!TryPreparePatch(request, out var family, out var deleteIds, out var upserts, out var error)) return new(VectorIndexStatus.InvalidRequest, Error: error);
        try { await writeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = Volatile.Read(ref state);
            var records = new Dictionary<Guid, VectorRecord>(current.Records);
            var deleted = 0L;
            foreach (var id in deleteIds)
            {
                if (records.TryGetValue(id, out var existing) && VectorFamilyKey.From(existing).Equals(family))
                {
                    records.Remove(id);
                    deleted++;
                }
            }

            foreach (var upsert in upserts)
            {
                if (records.TryGetValue(upsert.Id, out var byId))
                {
                    if (!VectorFamilyKey.From(byId).Equals(family)) return new(VectorIndexStatus.InvalidRequest, Error: "The vector patch conflicts with another family.");
                    records.Remove(upsert.Id);
                }

                var sameIdentity = records.Values.FirstOrDefault(record => string.Equals(Identity(record), Identity(upsert), StringComparison.Ordinal));
                if (sameIdentity is not null)
                {
                    if (!VectorFamilyKey.From(sameIdentity).Equals(family)) return new(VectorIndexStatus.InvalidRequest, Error: "The vector patch conflicts with another family.");
                    records.Remove(sameIdentity.Id);
                }
                records.Add(upsert.Id, upsert);
            }

            if (records.Count > options.MaxRecords) return new(VectorIndexStatus.TooLarge, Error: "The vector record limit was reached.");
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref state, IndexState.Create(records));
            return new(VectorIndexStatus.Success, deleted, upserts.Count);
        }
        catch (OperationCanceledException) { return new(VectorIndexStatus.Cancelled); }
        finally { writeGate.Release(); }
    }

    public Task<VectorGetResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new VectorGetResult(VectorIndexStatus.Cancelled));
        var current = Volatile.Read(ref state);
        return current.Records.TryGetValue(id, out var record) ? Task.FromResult(new VectorGetResult(VectorIndexStatus.Success, Copy(record))) : Task.FromResult(new VectorGetResult(VectorIndexStatus.NotFound));
    }

    public Task<bool> ContainsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Volatile.Read(ref state).Records.ContainsKey(id));
    }

    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((long)Volatile.Read(ref state).Records.Count);
    }

    public Task<VectorQueryResult> QueryAsync(VectorQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Cancelled));
        if (query.Limit <= 0 || query.Offset < 0) return Task.FromResult(new VectorQueryResult(VectorIndexStatus.InvalidRequest, Error: "The vector query is invalid."));
        try
        {
            var current = Volatile.Read(ref state);
            var matches = current.Records.Values.Where(record => Matches(record, query, cancellationToken)).OrderByDescending(record => record.UpdatedAt).ThenBy(record => record.Id).Skip(query.Offset).Take(Math.Min(query.Limit, options.MaxQueryResults)).Select(Copy).ToArray();
            return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Success, matches));
        }
        catch (OperationCanceledException) { return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Cancelled)); }
        catch { return Task.FromResult(new VectorQueryResult(VectorIndexStatus.Failed, Error: "Vector query failed.")); }
    }

    private bool TryPrepare(VectorRecord? candidate, out VectorRecord record, out string error)
    {
        record = default!; error = "The vector add request is invalid.";
        if (candidate is null || candidate.Id == Guid.Empty || string.IsNullOrWhiteSpace(candidate.Provider) || candidate.Embedding is null || !candidate.Embedding.IsValid() || !ValidMetadata(candidate.Metadata) || !string.Equals(candidate.ContentHash, candidate.Embedding.ContentHash, StringComparison.Ordinal)) return false;
        var now = DateTimeOffset.UtcNow;
        record = new(candidate.Id, candidate.Provider, candidate.Embedding, candidate.MemoryDocumentId, candidate.KnowledgeDocumentId, candidate.MemoryItemId, candidate.KnowledgeItemId, candidate.Embedding.ContentHash, now, now, CopyMetadata(candidate.Metadata));
        return true;
    }

    private bool TryPrepareReplacement(VectorReplaceRequest request, out VectorFamilyKey family, out IReadOnlyList<VectorRecord> records, out string error)
    {
        family = request.Family!; records = [] ; error = "The vector replacement request is invalid.";
        if (request.Family is null || !request.Family.IsValid() || request.Records is null) return false;
        var prepared = new List<VectorRecord>(); var ids = new HashSet<Guid>(); var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in request.Records)
        {
            if (!TryPrepare(candidate, out var record, out _)
                || !VectorFamilyKey.From(record).Equals(request.Family)
                || !ids.Add(record.Id)
                || !identities.Add(Identity(record))) return false;
            prepared.Add(record);
        }
        if (prepared.Count > options.MaxRecords) return false;
        family = request.Family; records = prepared;
        return true;
    }

    private bool TryPreparePatch(VectorFamilyPatchRequest request, out VectorFamilyKey family, out IReadOnlyList<Guid> deleteIds, out IReadOnlyList<VectorRecord> upserts, out string error)
    {
        family = request.Family!;
        deleteIds = [];
        upserts = [];
        error = "The vector patch request is invalid.";
        if (request.Family is null || !request.Family.IsValid() || request.DeleteIds is null || request.Upserts is null) return false;
        if (request.DeleteIds.Any(id => id == Guid.Empty)) return false;
        var prepared = new List<VectorRecord>();
        var ids = new HashSet<Guid>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in request.Upserts)
        {
            if (!TryPrepare(candidate, out var record, out _)
                || !VectorFamilyKey.From(record).Equals(request.Family)
                || !ids.Add(record.Id)
                || !identities.Add(Identity(record))) return false;
            prepared.Add(record);
        }
        family = request.Family;
        deleteIds = request.DeleteIds.Distinct().ToArray();
        upserts = prepared;
        return true;
    }

    private bool ValidMetadata(IReadOnlyDictionary<string, string>? metadata) => metadata is null || (metadata.Count <= options.MaxMetadataEntries && metadata.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null));
    private static Dictionary<string, string>? CopyMetadata(IReadOnlyDictionary<string, string>? metadata) => metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    private static VectorRecord Copy(VectorRecord record) => record with { Metadata = CopyMetadata(record.Metadata) };
    private static string Identity(VectorRecord record) => string.Concat(
        record.Embedding.InputId.ToString("N"), ":",
        Encode(record.Provider), ":", Encode(record.Embedding.Model), ":",
        record.Embedding.ModelVersion is null ? "N" : "V" + Encode(record.Embedding.ModelVersion), ":",
        record.Embedding.Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static bool Matches(VectorRecord record, VectorQuery query, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var embedding = record.Embedding;
        return (!query.Id.HasValue || record.Id == query.Id) && (!query.InputId.HasValue || embedding.InputId == query.InputId) && (!query.MemoryDocumentId.HasValue || record.MemoryDocumentId == query.MemoryDocumentId) && (!query.KnowledgeDocumentId.HasValue || record.KnowledgeDocumentId == query.KnowledgeDocumentId) && (!query.MemoryItemId.HasValue || record.MemoryItemId == query.MemoryItemId) && (!query.KnowledgeItemId.HasValue || record.KnowledgeItemId == query.KnowledgeItemId) && (query.Provider is null || record.Provider == query.Provider) && (query.Model is null || embedding.Model == query.Model) && (query.Version is null || embedding.ModelVersion == query.Version) && (!query.Dimensions.HasValue || embedding.Dimensions == query.Dimensions) && (query.ContentHash is null || record.ContentHash == query.ContentHash) && (query.MetadataKey is null || record.Metadata?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);
    }

    public void Dispose() => writeGate.Dispose();

    private sealed class IndexState
    {
        private IndexState(Dictionary<Guid, VectorRecord> records, Dictionary<string, Guid> identities) { Records = records; Identities = identities; }
        internal static IndexState Empty { get; } = new([], new(StringComparer.Ordinal));
        internal Dictionary<Guid, VectorRecord> Records { get; }
        internal Dictionary<string, Guid> Identities { get; }
        internal static IndexState Create(Dictionary<Guid, VectorRecord> records)
        {
            var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var pair in records) identities.Add(Identity(pair.Value), pair.Key);
            return new(records, identities);
        }
    }
}
