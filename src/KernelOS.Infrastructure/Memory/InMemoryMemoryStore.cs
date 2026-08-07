using System.Collections.Concurrent;
using KernelOS.Core.Memory;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Memory;

public sealed class InMemoryMemoryStore : IMemoryStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, MemoryDocument> documents = new();
    private readonly ConcurrentDictionary<Guid, Guid> knowledgeDocumentIds = new();
    private readonly MemoryOptionsSnapshot options;
    private readonly SemaphoreSlim storeGate = new(1, 1);

    public InMemoryMemoryStore(IOptions<MemoryOptions> options)
    {
        var value = options.Value;
        this.options = new(value.MaxDocuments, value.MaxItemsPerDocument, value.MaxQueryResults);
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
            return new(MemoryStatus.Success, MemoryDocumentFactory.Copy(document));
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch { return new(MemoryStatus.Failed, Error: "Memory storage failed."); }
        finally { storeGate.Release(); }
    }

    public Task<MemoryUpdateResult> UpdateAsync(MemoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new MemoryUpdateResult(MemoryStatus.Cancelled));
        if (!Guid.TryParse(request.Id, out var id) || request.Items is null || request.Metadata is null || request.Items.Count > options.MaxItemsPerDocument) return Task.FromResult(new MemoryUpdateResult(MemoryStatus.InvalidRequest, Error: "The memory update request is invalid."));
        try
        {
            while (documents.TryGetValue(id, out var current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var updated = MemoryDocumentFactory.Update(current, request.Items, request.Metadata, DateTimeOffset.UtcNow);
                if (documents.TryUpdate(id, updated, current)) return Task.FromResult(new MemoryUpdateResult(MemoryStatus.Success, MemoryDocumentFactory.Copy(updated)));
            }
            return Task.FromResult(new MemoryUpdateResult(MemoryStatus.NotFound));
        }
        catch (OperationCanceledException) { return Task.FromResult(new MemoryUpdateResult(MemoryStatus.Cancelled)); }
        catch { return Task.FromResult(new MemoryUpdateResult(MemoryStatus.Failed, Error: "Memory update failed.")); }
    }

    public Task<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new MemoryDeleteResult(MemoryStatus.Cancelled));
        if (!Guid.TryParse(request.Id, out var id)) return Task.FromResult(new MemoryDeleteResult(MemoryStatus.InvalidRequest, "The memory delete request is invalid."));
        if (!documents.TryRemove(id, out var removed)) return Task.FromResult(new MemoryDeleteResult(MemoryStatus.NotFound));
        knowledgeDocumentIds.TryRemove(removed.KnowledgeDocumentId, out _);
        return Task.FromResult(new MemoryDeleteResult(MemoryStatus.Success));
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

    private static bool Matches(MemoryDocument document, MemoryQuery query) =>
        (query.Id is null || string.Equals(document.Id.ToString(), query.Id, StringComparison.OrdinalIgnoreCase))
        && (!query.KnowledgeDocumentId.HasValue || document.KnowledgeDocumentId == query.KnowledgeDocumentId)
        && (query.ContentHash is null || document.ContentHash == query.ContentHash || document.Items.Any(item => item.ContentHash == query.ContentHash))
        && (query.ItemType is null || document.Items.Any(item => item.Type == query.ItemType))
        && (query.ExactContent is null || document.Items.Any(item => item.Content == query.ExactContent))
        && (query.MetadataKey is null || document.Metadata.Properties?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);

    public void Dispose() => storeGate.Dispose();
}
