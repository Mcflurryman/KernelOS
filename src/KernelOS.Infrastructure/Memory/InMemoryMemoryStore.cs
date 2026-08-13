using System.Collections.Concurrent;
using KernelOS.Core.Memory;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Memory;

public sealed class InMemoryMemoryStore : IMemoryStore, IMemorySnapshotProvider, IDisposable
{
    private readonly ConcurrentDictionary<Guid, MemoryDocument> documents = new();
    private readonly ConcurrentDictionary<Guid, Guid> knowledgeDocumentIds = new();
    private readonly MemoryOptionsSnapshot options;
    private readonly IMemoryMutationObserver mutationObserver;
    private readonly SemaphoreSlim storeGate = new(1, 1);

    public InMemoryMemoryStore(IOptions<MemoryOptions> options) : this(options, NullMemoryMutationObserver.Instance)
    {
    }

    public InMemoryMemoryStore(IOptions<MemoryOptions> options, IMemoryMutationObserver mutationObserver)
    {
        var value = options.Value;
        this.options = new(value.MaxDocuments, value.MaxItemsPerDocument, value.MaxQueryResults);
        this.mutationObserver = mutationObserver;
    }

    public async Task<MemoryStoreResult> StoreAsync(MemoryStoreRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (request.KnowledgeDocument is null || request.KnowledgeDocument.Items.Count > options.MaxItemsPerDocument) return new(MemoryStatus.InvalidRequest, Error: "The memory store request is invalid.");

        var document = MemoryDocumentFactory.Create(request.KnowledgeDocument, DateTimeOffset.UtcNow);
        try { await storeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        try
        {
            if (documents.Count >= options.MaxDocuments) return new(MemoryStatus.InvalidRequest, Error: "The memory document limit was reached.");
            if (!knowledgeDocumentIds.TryAdd(document.KnowledgeDocumentId, document.Id)) return new(MemoryStatus.AlreadyExists);
            if (!documents.TryAdd(document.Id, document)) { knowledgeDocumentIds.TryRemove(document.KnowledgeDocumentId, out _); return new(MemoryStatus.AlreadyExists); }
            var current = MemoryDocumentFactory.Copy(document);
            await NotifyCommittedAsync(new(MemoryMutationType.Created, null, MemoryDocumentFactory.Copy(document), DateTimeOffset.UtcNow));
            return new(MemoryStatus.Success, current);
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch { return new(MemoryStatus.Failed, Error: "Memory storage failed."); }
        finally { storeGate.Release(); }
    }

    public async Task<MemoryUpdateResult> UpdateAsync(MemoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (!Guid.TryParse(request.Id, out var id) || request.Items is null || request.Metadata is null || request.Items.Count > options.MaxItemsPerDocument) return new(MemoryStatus.InvalidRequest, Error: "The memory update request is invalid.");
        try { await storeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        try
        {
            while (documents.TryGetValue(id, out var current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var updated = MemoryDocumentFactory.Update(current, request.Items, request.Metadata, DateTimeOffset.UtcNow);
                if (documents.TryUpdate(id, updated, current))
                {
                    var previous = MemoryDocumentFactory.Copy(current);
                    var committed = MemoryDocumentFactory.Copy(updated);
                    await NotifyCommittedAsync(new(MemoryMutationType.Updated, MemoryDocumentFactory.Copy(current), MemoryDocumentFactory.Copy(updated), DateTimeOffset.UtcNow));
                    return new(MemoryStatus.Success, committed);
                }
            }
            return new(MemoryStatus.NotFound);
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch { return new(MemoryStatus.Failed, Error: "Memory update failed."); }
        finally { storeGate.Release(); }
    }

    public async Task<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (!Guid.TryParse(request.Id, out var id)) return new(MemoryStatus.InvalidRequest, "The memory delete request is invalid.");
        try { await storeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        try
        {
            if (!documents.TryRemove(id, out var removed)) return new(MemoryStatus.NotFound);
            knowledgeDocumentIds.TryRemove(removed.KnowledgeDocumentId, out _);
            await NotifyCommittedAsync(new(MemoryMutationType.Deleted, MemoryDocumentFactory.Copy(removed), null, DateTimeOffset.UtcNow));
            return new(MemoryStatus.Success);
        }
        finally { storeGate.Release(); }
    }

    public Task<MemoryGetResult> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new MemoryGetResult(MemoryStatus.Cancelled));
        return Guid.TryParse(id, out var parsed) && documents.TryGetValue(parsed, out var document)
            ? Task.FromResult(new MemoryGetResult(MemoryStatus.Success, MemoryDocumentFactory.Copy(document)))
            : Task.FromResult(new MemoryGetResult(MemoryStatus.NotFound));
    }

    public Task<MemoryQueryResult> QueryAsync(MemoryQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new MemoryQueryResult(MemoryStatus.Cancelled));
        if (query.Limit <= 0 || query.Offset < 0) return Task.FromResult(new MemoryQueryResult(MemoryStatus.InvalidRequest, Error: "The memory query is invalid."));
        try
        {
            var limit = Math.Min(query.Limit, options.MaxQueryResults);
            var results = documents.Values.Where(document => Matches(document, query)).OrderByDescending(document => document.UpdatedAt).ThenBy(document => document.Id).Skip(query.Offset).Take(limit).Select(MemoryDocumentFactory.Copy).ToArray();
            return Task.FromResult(new MemoryQueryResult(MemoryStatus.Success, results));
        }
        catch { return Task.FromResult(new MemoryQueryResult(MemoryStatus.Failed, Error: "Memory query failed.")); }
    }

    public async Task<MemorySnapshotResult> CreateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        try { await storeGate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentsSnapshot = documents.Values
                .OrderByDescending(document => document.UpdatedAt)
                .ThenBy(document => document.Id)
                .Select(MemoryDocumentFactory.Copy)
                .ToArray();
            return new(MemoryStatus.Success, new MemorySnapshot(documentsSnapshot, DateTimeOffset.UtcNow));
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch { return new(MemoryStatus.Failed, Error: "Memory snapshot creation failed."); }
        finally { storeGate.Release(); }
    }

    private static bool Matches(MemoryDocument document, MemoryQuery query) =>
        (query.Id is null || string.Equals(document.Id.ToString(), query.Id, StringComparison.OrdinalIgnoreCase))
        && (!query.KnowledgeDocumentId.HasValue || document.KnowledgeDocumentId == query.KnowledgeDocumentId)
        && (!query.MemoryItemId.HasValue || document.Items.Any(item => item.Id == query.MemoryItemId))
        && (query.ContentHash is null || document.ContentHash == query.ContentHash || document.Items.Any(item => item.ContentHash == query.ContentHash))
        && (query.ItemType is null || document.Items.Any(item => item.Type == query.ItemType))
        && (query.ExactContent is null || document.Items.Any(item => item.Content == query.ExactContent))
        && (query.MetadataKey is null || document.Metadata.Properties?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);

    public void Dispose() => storeGate.Dispose();

    private async Task NotifyCommittedAsync(MemoryMutationCommitted mutation)
    {
        try { await mutationObserver.ObserveAsync(mutation, CancellationToken.None); }
        catch { }
    }
}
