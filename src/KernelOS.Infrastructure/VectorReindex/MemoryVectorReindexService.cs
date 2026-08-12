using System.Security.Cryptography;
using System.Text;
using KernelOS.Core.Embeddings;
using KernelOS.Core.Memory;
using KernelOS.Core.VectorIndex;
using KernelOS.Core.VectorReindex;
using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure.VectorReindex;

public sealed class MemoryVectorReindexService : IVectorReindexService, IDisposable
{
    private const int DefaultBatchSize = 32;
    private readonly IMemorySnapshotProvider snapshots;
    private readonly IVectorIndex vectorIndex;
    private readonly IEmbeddingGenerator[] generators;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<MemoryVectorReindexService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    public MemoryVectorReindexService(
        IMemorySnapshotProvider snapshots,
        IVectorIndex vectorIndex,
        IEnumerable<IEmbeddingGenerator> generators,
        TimeProvider timeProvider,
        ILogger<MemoryVectorReindexService> logger)
    {
        this.snapshots = snapshots;
        this.vectorIndex = vectorIndex;
        this.generators = generators.ToArray();
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<VectorReindexResult> ReindexAsync(CancellationToken cancellationToken = default)
    {
        var acquired = false;
        try
        {
            acquired = await gate.WaitAsync(0, cancellationToken);
            if (!acquired) return new(VectorReindexStatus.AlreadyRunning);
            if (cancellationToken.IsCancellationRequested) return new(VectorReindexStatus.Cancelled);
            if (generators.Length != 1) return Failure("EMBEDDING_GENERATOR_UNAVAILABLE");

            var generator = generators[0];
            var model = generator.ModelInfo;
            if (string.IsNullOrWhiteSpace(model.Provider) || string.IsNullOrWhiteSpace(model.Model) || model.Dimensions <= 0)
                return Failure("EMBEDDING_MODEL_INVALID", model);

            var family = new VectorFamilyKey(model.Provider, model.Model, model.Version, model.Dimensions);
            var startedAt = timeProvider.GetUtcNow();
            var startedTimestamp = timeProvider.GetTimestamp();
            var snapshotResult = await snapshots.CreateSnapshotAsync(cancellationToken);
            if (snapshotResult.Status == MemoryStatus.Cancelled) return Complete(VectorReindexStatus.Cancelled, model, startedAt, null, timeProvider.GetElapsedTime(startedTimestamp));
            if (snapshotResult.Status != MemoryStatus.Success || snapshotResult.Snapshot is null) return Complete(VectorReindexStatus.Failed, model, startedAt, null, timeProvider.GetElapsedTime(startedTimestamp), ErrorCode: "MEMORY_SNAPSHOT_FAILED");

            var snapshot = snapshotResult.Snapshot;
            var baseResult = new VectorReindexResult(VectorReindexStatus.Failed, model.Provider, model.Model, model.Version, model.Dimensions, snapshot.TotalDocuments, snapshot.TotalItems, StartedAt: startedAt, CapturedAt: snapshot.CapturedAt);
            var inputs = snapshot.Documents.SelectMany(document => document.Items.Select(item => new ItemInput(document, item))).ToArray();
            if (inputs.Select(value => value.Item.Id).Distinct().Count() != inputs.Length)
                return baseResult with { FailedItems = inputs.Length, Duration = timeProvider.GetElapsedTime(startedTimestamp), ErrorCode = "MEMORY_ITEM_ID_DUPLICATE" };
            var records = new List<VectorRecord>(inputs.Length);
            var processed = 0;

            foreach (var batch in inputs.Chunk(DefaultBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requested = batch.Select(value => new EmbeddingInput(value.Item.Id, value.Item.Content, value.Item.ContentHash)).ToArray();
                var batchResult = await generator.GenerateBatchAsync(new(requested), cancellationToken);
                if (batchResult.Status == EmbeddingStatus.Cancelled || cancellationToken.IsCancellationRequested)
                    return baseResult with { Status = VectorReindexStatus.Cancelled, ProcessedItems = processed, Duration = timeProvider.GetElapsedTime(startedTimestamp) };
                if (batchResult.Status != EmbeddingStatus.Success || batchResult.Results is null)
                    return baseResult with { ProcessedItems = processed, FailedItems = batch.Length, Duration = timeProvider.GetElapsedTime(startedTimestamp), ErrorCode = "EMBEDDING_BATCH_FAILED" };

                if (!TryCorrelate(batch, batchResult.Results, model, out var vectors))
                    return baseResult with { ProcessedItems = processed, FailedItems = batch.Length, Duration = timeProvider.GetElapsedTime(startedTimestamp), ErrorCode = "EMBEDDING_RESULT_INVALID" };

                foreach (var value in batch)
                {
                    var vector = vectors[value.Item.Id];
                    records.Add(new VectorRecord(
                        CreateRecordId(value.Item.Id, family), model.Provider, vector, value.Document.Id, value.Document.KnowledgeDocumentId,
                        value.Item.Id, value.Item.KnowledgeItemId, value.Item.ContentHash, startedAt, startedAt));
                }
                processed += batch.Length;
            }

            var replace = await vectorIndex.ReplaceFamilyAsync(new(family, records), cancellationToken);
            if (replace.Status == VectorIndexStatus.Cancelled)
                return baseResult with { Status = VectorReindexStatus.Cancelled, ProcessedItems = processed, IndexedItems = records.Count, Duration = timeProvider.GetElapsedTime(startedTimestamp) };
            if (replace.Status != VectorIndexStatus.Success)
                return baseResult with { ProcessedItems = processed, IndexedItems = records.Count, Duration = timeProvider.GetElapsedTime(startedTimestamp), ErrorCode = "VECTOR_REPLACEMENT_FAILED" };

            var status = inputs.Length == 0 ? VectorReindexStatus.NoMemory : VectorReindexStatus.Success;
            var duration = timeProvider.GetElapsedTime(startedTimestamp);
            VectorReindexLog.Completed(logger, model.Provider, model.Model, model.Version, model.Dimensions, records.Count, duration.TotalMilliseconds);
            return baseResult with { Status = status, ProcessedItems = processed, IndexedItems = records.Count, Duration = duration, Published = true };
        }
        catch (OperationCanceledException) { return new(VectorReindexStatus.Cancelled); }
        catch (Exception)
        {
            VectorReindexLog.Failed(logger);
            return new(VectorReindexStatus.Failed, ErrorCode: "REINDEX_FAILED");
        }
        finally { if (acquired) gate.Release(); }
    }

    private static bool TryCorrelate(IReadOnlyList<ItemInput> batch, IReadOnlyList<EmbeddingResult> results, EmbeddingModelInfo model, out Dictionary<Guid, EmbeddingVector> vectors)
    {
        vectors = new();
        if (results.Count != batch.Count) return false;
        var expected = batch.ToDictionary(value => value.Item.Id);
        foreach (var result in results)
        {
            var vector = result.Vector;
            if (result.Status != EmbeddingStatus.Success || vector is null || !vector.IsValid() || !expected.TryGetValue(vector.InputId, out var item)
                || !string.Equals(vector.ContentHash, item.Item.ContentHash, StringComparison.Ordinal)
                || !string.Equals(vector.Model, model.Model, StringComparison.Ordinal)
                || !string.Equals(vector.ModelVersion, model.Version, StringComparison.Ordinal)
                || vector.Dimensions != model.Dimensions || !vectors.TryAdd(vector.InputId, vector)) return false;
        }
        return vectors.Count == expected.Count;
    }

    private static VectorReindexResult Failure(string code, EmbeddingModelInfo? model = null) => new(VectorReindexStatus.Failed, model?.Provider, model?.Model, model?.Version, model?.Dimensions, ErrorCode: code);
    private static VectorReindexResult Complete(VectorReindexStatus status, EmbeddingModelInfo model, DateTimeOffset startedAt, DateTimeOffset? capturedAt, TimeSpan duration, string? ErrorCode = null) => new(status, model.Provider, model.Model, model.Version, model.Dimensions, StartedAt: startedAt, CapturedAt: capturedAt, Duration: duration, ErrorCode: ErrorCode);
    private static Guid CreateRecordId(Guid inputId, VectorFamilyKey family)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(
            inputId.ToString("N"), ":", Encode(family.Provider), ":", Encode(family.Model), ":",
            family.Version is null ? "N" : "V" + Encode(family.Version), ":", family.Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return new Guid(bytes.AsSpan(0, 16));
    }
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    public void Dispose() => gate.Dispose();
    private sealed record ItemInput(MemoryDocument Document, MemoryItem Item);
}

internal static partial class VectorReindexLog
{
    [LoggerMessage(EventId = 70, Level = LogLevel.Information, Message = "Semantic index rebuild completed for {Provider}/{Model}/{Version} ({Dimensions} dimensions): {IndexedItems} records in {DurationMilliseconds} ms.")]
    internal static partial void Completed(ILogger logger, string provider, string model, string? version, int dimensions, int indexedItems, double durationMilliseconds);
    [LoggerMessage(EventId = 71, Level = LogLevel.Warning, Message = "Semantic index rebuild failed.")]
    internal static partial void Failed(ILogger logger);
}
