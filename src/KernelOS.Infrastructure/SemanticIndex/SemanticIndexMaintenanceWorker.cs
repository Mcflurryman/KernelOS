using KernelOS.Core.Embeddings;
using KernelOS.Core.Memory;
using KernelOS.Core.SemanticIndex;
using KernelOS.Core.VectorIndex;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure.SemanticIndex;

public sealed class SemanticIndexMaintenanceWorker : BackgroundService
{
    private const int EmbeddingBatchSize = 32;
    private readonly SemanticMutationBuffer queue;
    private readonly ISemanticIndexCoordinator coordinator;
    private readonly IVectorIndex vectorIndex;
    private readonly IEmbeddingGenerator[] generators;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SemanticIndexMaintenanceWorker> logger;

    public SemanticIndexMaintenanceWorker(SemanticMutationBuffer queue, ISemanticIndexCoordinator coordinator, IVectorIndex vectorIndex, IEnumerable<IEmbeddingGenerator> generators, TimeProvider timeProvider, ILogger<SemanticIndexMaintenanceWorker> logger)
    {
        this.queue = queue; this.coordinator = coordinator; this.vectorIndex = vectorIndex; this.generators = generators.ToArray(); this.timeProvider = timeProvider; this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            while (queue.Reader.TryRead(out var queued)) await ProcessSafelyAsync(queued, stoppingToken);
        }
    }

    internal async Task ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var queued = await queue.Reader.ReadAsync(cancellationToken);
        await ProcessSafelyAsync(queued, cancellationToken);
    }

    private async Task ProcessSafelyAsync(QueuedMemoryMutation queued, CancellationToken cancellationToken)
    {
        try { await ProcessAsync(queued, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { coordinator.MarkDirty(); SemanticIndexMaintenanceLog.Failed(logger, queued.Generation, queued.Mutation.Type); }
    }

    private async Task ProcessAsync(QueuedMemoryMutation queued, CancellationToken cancellationToken)
    {
        var state = coordinator.GetSnapshot();
        if (queued.Generation <= state.AppliedGeneration || state.Status is SemanticIndexStatus.NeedsRebuild or SemanticIndexStatus.Building or SemanticIndexStatus.Dirty) return;
        if (state.Status != SemanticIndexStatus.Maintaining || queued.Generation != state.AppliedGeneration + 1 || state.ReadyFamily is null) { coordinator.MarkDirty(); return; }
        if (generators.Length != 1) { coordinator.MarkDirty(); return; }
        var generator = generators[0];
        var model = generator.ModelInfo;
        if (string.IsNullOrWhiteSpace(model.Provider) || string.IsNullOrWhiteSpace(model.Model) || model.Dimensions <= 0) { coordinator.MarkDirty(); return; }
        var family = new VectorFamilyKey(model.Provider, model.Model, model.Version, model.Dimensions);
        if (!family.Equals(state.ReadyFamily)) { coordinator.MarkDirty(); return; }

        var (document, embed, unchanged, reuse, deletes) = Diff(queued.Mutation, family);
        var reusedRecords = await ReuseExistingAsync(document, unchanged, reuse, family, cancellationToken);
        if (reusedRecords is null) { coordinator.MarkDirty(); return; }
        var vectors = await GenerateAsync(generator, model, embed, cancellationToken);
        if (vectors is null) { coordinator.MarkDirty(); return; }
        var now = timeProvider.GetUtcNow();
        var upserts = reusedRecords.Concat(embed.Select(item => SemanticVectorRecords.Create(document!, item, vectors[item.Id], model, family, now))).ToArray();
        var patch = await vectorIndex.ApplyFamilyPatchAsync(new(family, deletes, upserts), cancellationToken);
        if (patch.Status != VectorIndexStatus.Success || !coordinator.CompleteIncremental(queued.Generation, family))
        {
            coordinator.MarkDirty();
            SemanticIndexMaintenanceLog.Failed(logger, queued.Generation, queued.Mutation.Type);
        }
    }

    private static (MemoryDocument? Document, IReadOnlyList<MemoryItem> Embed, IReadOnlyList<MemoryItem> Unchanged, IReadOnlyList<MemoryItem> Reuse, IReadOnlyList<Guid> Deletes) Diff(MemoryMutationCommitted mutation, VectorFamilyKey family)
    {
        if (mutation.Type == MemoryMutationType.Deleted)
            return (null, [], [], [], mutation.Previous!.Items.Select(item => SemanticVectorRecords.CreateRecordId(item.Id, family)).ToArray());
        var current = mutation.Current!;
        if (mutation.Type == MemoryMutationType.Created) return (current, current.Items, [], [], []);
        var previous = mutation.Previous!.Items.ToDictionary(item => item.Id);
        var currentIds = current.Items.Select(item => item.Id).ToHashSet();
        var embed = current.Items.Where(item => !previous.TryGetValue(item.Id, out var old) || !string.Equals(old.ContentHash, item.ContentHash, StringComparison.Ordinal)).ToArray();
        var unchanged = current.Items.Where(item => previous.TryGetValue(item.Id, out var old) && string.Equals(old.ContentHash, item.ContentHash, StringComparison.Ordinal)).ToArray();
        var reuse = current.Items.Where(item => previous.TryGetValue(item.Id, out var old) && string.Equals(old.ContentHash, item.ContentHash, StringComparison.Ordinal) && old.KnowledgeItemId != item.KnowledgeItemId).ToArray();
        var deletes = previous.Values.Where(item => !currentIds.Contains(item.Id)).Select(item => SemanticVectorRecords.CreateRecordId(item.Id, family)).ToArray();
        return (current, embed, unchanged, reuse, deletes);
    }

    private async Task<IReadOnlyList<VectorRecord>?> ReuseExistingAsync(MemoryDocument? document, IReadOnlyList<MemoryItem> unchanged, IReadOnlyList<MemoryItem> reuse, VectorFamilyKey family, CancellationToken cancellationToken)
    {
        if (unchanged.Count == 0) return [];
        var reuseIds = reuse.Select(item => item.Id).ToHashSet();
        var records = new List<VectorRecord>(reuse.Count);
        foreach (var item in unchanged)
        {
            var existing = await vectorIndex.GetAsync(SemanticVectorRecords.CreateRecordId(item.Id, family), cancellationToken);
            if (existing.Status != VectorIndexStatus.Success || existing.Record is null) return null;
            if (reuseIds.Contains(item.Id)) records.Add(existing.Record with { MemoryDocumentId = document!.Id, KnowledgeDocumentId = document.KnowledgeDocumentId, MemoryItemId = item.Id, KnowledgeItemId = item.KnowledgeItemId, UpdatedAt = timeProvider.GetUtcNow() });
        }
        return records;
    }

    private static async Task<Dictionary<Guid, EmbeddingVector>?> GenerateAsync(IEmbeddingGenerator generator, EmbeddingModelInfo model, IReadOnlyList<MemoryItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return [];
        var vectors = new Dictionary<Guid, EmbeddingVector>();
        foreach (var batch in items.Chunk(EmbeddingBatchSize))
        {
            var result = await generator.GenerateBatchAsync(new(batch.Select(item => new EmbeddingInput(item.Id, item.Content, item.ContentHash)).ToArray()), cancellationToken);
            if (result.Status != EmbeddingStatus.Success || result.Results is null || !SemanticVectorRecords.TryCorrelate(batch, result.Results, model, out var current)) return null;
            foreach (var pair in current) if (!vectors.TryAdd(pair.Key, pair.Value)) return null;
        }
        return vectors;
    }
}

internal static partial class SemanticIndexMaintenanceLog
{
    [LoggerMessage(EventId = 81, Level = LogLevel.Warning, Message = "Semantic maintenance failed at generation {Generation} for {MutationType}; a rebuild is required.")]
    internal static partial void Failed(ILogger logger, long generation, MemoryMutationType mutationType);
}
