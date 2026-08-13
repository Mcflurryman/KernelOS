using KernelOS.Core.Embeddings;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Core.SemanticIndex;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.Memory;
using KernelOS.Infrastructure.SemanticIndex;
using KernelOS.Infrastructure.VectorIndex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class SemanticIndexMaintenanceWorkerTests
{
    [Fact]
    public async Task CreateUpdateAndDeleteApplyOrderedIncrementalPatches()
    {
        var coordinator = new SemanticIndexCoordinator();
        var generator = new Generator();
        using var index = Index();
        var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue);
        var worker = Worker(queue, coordinator, index, generator);
        MakeReady(coordinator, generator.ModelInfo);

        var created = (await store.StoreAsync(new(Document("A", "B", "C")))).Document!;
        await worker.ProcessNextAsync();
        var nextItems = new[]
        {
            created.Items[0],
            created.Items[1] with { Content = "B2", ContentHash = "B2" },
            MemoryItem("D", created.Metadata)
        };
        await store.UpdateAsync(new(created.Id.ToString(), nextItems, created.Metadata));
        await worker.ProcessNextAsync();
        await store.DeleteAsync(new(created.Id.ToString()));
        await worker.ProcessNextAsync();

        Assert.Equal(2, generator.BatchCalls);
        Assert.Equal(0, await index.CountAsync());
        var state = coordinator.GetSnapshot();
        Assert.Equal(SemanticIndexStatus.Ready, state.Status);
        Assert.Equal(state.CurrentGeneration, state.AppliedGeneration);
    }

    [Fact]
    public async Task FailedEmbeddingMarksDirtyWithoutChangingDurableMemory()
    {
        var coordinator = new SemanticIndexCoordinator();
        var generator = new Generator(fail: true);
        using var index = Index();
        var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue);
        var worker = Worker(queue, coordinator, index, generator);
        MakeReady(coordinator, generator.ModelInfo);

        var stored = await store.StoreAsync(new(Document("content")));
        await worker.ProcessNextAsync();

        Assert.Equal(MemoryStatus.Success, stored.Status);
        Assert.Equal(0, await index.CountAsync());
        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public async Task NeedsRebuildDiscardsMutationsWithoutGeneratingEmbeddings()
    {
        var coordinator = new SemanticIndexCoordinator();
        var generator = new Generator();
        using var index = Index();
        var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue);
        var worker = Worker(queue, coordinator, index, generator);

        await store.StoreAsync(new(Document("content")));
        await worker.ProcessNextAsync();

        Assert.Equal(0, generator.BatchCalls);
        Assert.Equal(0, await index.CountAsync());
        Assert.Equal(SemanticIndexStatus.NeedsRebuild, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public async Task FullQueueMarksDirtyWhileMemoryWritesRemainSuccessful()
    {
        var coordinator = new SemanticIndexCoordinator();
        var queue = new SemanticMutationBuffer(coordinator, Options.Create(new SemanticIndexMaintenanceOptions { QueueCapacity = 1 }), NullLogger<SemanticMutationBuffer>.Instance);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue);

        var first = await store.StoreAsync(new(Document("first")));
        var second = await store.StoreAsync(new(Document("second")));

        Assert.Equal(MemoryStatus.Success, first.Status);
        Assert.Equal(MemoryStatus.Success, second.Status);
        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public async Task ChangedActiveFamilyMarksDirtyWithoutPartialPatch()
    {
        var coordinator = new SemanticIndexCoordinator();
        var familyA = new Generator(model: "a"); var familyB = new Generator(model: "b");
        using var index = Index(); var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue);
        MakeReady(coordinator, familyA.ModelInfo);
        var worker = Worker(queue, coordinator, index, familyB);

        await store.StoreAsync(new(Document("content")));
        await worker.ProcessNextAsync();

        Assert.Equal(0, familyB.BatchCalls);
        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public async Task GenerationGapMarksDirtyWithoutApplyingStalePatch()
    {
        var coordinator = new SemanticIndexCoordinator(); var generator = new Generator();
        using var index = Index(); var queue = Queue(coordinator); var worker = Worker(queue, coordinator, index, generator);
        MakeReady(coordinator, generator.ModelInfo);
        await queue.ObserveAsync(new(MemoryMutationType.Created, null, new MemoryDocument(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new(1, DateTimeOffset.UtcNow, "h"), [], new("text/plain"), "h"), DateTimeOffset.UtcNow));
        await queue.ObserveAsync(new(MemoryMutationType.Created, null, new MemoryDocument(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new(1, DateTimeOffset.UtcNow, "h"), [], new("text/plain"), "h"), DateTimeOffset.UtcNow));
        await queue.Reader.ReadAsync();

        await worker.ProcessNextAsync();

        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
        Assert.Equal(0, await index.CountAsync());
    }

    [Fact]
    public async Task MissingUnchangedVectorMarksDirtyInsteadOfSilentlySkippingIt()
    {
        var coordinator = new SemanticIndexCoordinator(); var generator = new Generator();
        using var index = Index(); var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptions()), queue); var worker = Worker(queue, coordinator, index, generator);
        MakeReady(coordinator, generator.ModelInfo);
        var created = (await store.StoreAsync(new(Document("content")))).Document!;
        await worker.ProcessNextAsync();
        var family = new VectorFamilyKey(generator.ModelInfo.Provider, generator.ModelInfo.Model, generator.ModelInfo.Version, generator.ModelInfo.Dimensions);
        await index.DeleteAsync(new(SemanticVectorRecords.CreateRecordId(created.Items[0].Id, family)));
        await store.UpdateAsync(new(created.Id.ToString(), created.Items, created.Metadata));

        await worker.ProcessNextAsync();

        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public async Task LargeCreateUsesBatchesOfThirtyTwo()
    {
        var coordinator = new SemanticIndexCoordinator(); var generator = new Generator();
        using var index = Index(); var queue = Queue(coordinator);
        using var store = new InMemoryMemoryStore(Options.Create(new MemoryOptions { MaxDocuments = 10, MaxItemsPerDocument = 200, MaxQueryResults = 10 }), queue); var worker = Worker(queue, coordinator, index, generator);
        MakeReady(coordinator, generator.ModelInfo);

        await store.StoreAsync(new(Document(Enumerable.Range(0, 100).Select(number => $"item-{number}").ToArray())));
        await worker.ProcessNextAsync();

        Assert.Equal(4, generator.BatchCalls);
        Assert.Equal(100, await index.CountAsync());
    }

    private static SemanticMutationBuffer Queue(ISemanticIndexCoordinator coordinator) => new(coordinator, Options.Create(new SemanticIndexMaintenanceOptions { QueueCapacity = 8 }), NullLogger<SemanticMutationBuffer>.Instance);
    private static SemanticIndexMaintenanceWorker Worker(SemanticMutationBuffer queue, ISemanticIndexCoordinator coordinator, IVectorIndex index, IEmbeddingGenerator generator) => new(queue, coordinator, index, [generator], TimeProvider.System, NullLogger<SemanticIndexMaintenanceWorker>.Instance);
    private static InMemoryVectorIndex Index() => new(Options.Create(new VectorIndexOptions { MaxRecords = 100, MaxQueryResults = 100, MaxMetadataEntries = 10 }));
    private static MemoryOptions MemoryOptions() => new() { MaxDocuments = 10, MaxItemsPerDocument = 10, MaxQueryResults = 10 };
    private static void MakeReady(SemanticIndexCoordinator coordinator, EmbeddingModelInfo model) => coordinator.CompleteRebuild(coordinator.BeginRebuild(), new(model.Provider, model.Model, model.Version, model.Dimensions));
    private static KnowledgeDocument Document(params string[] values)
    {
        var id = Guid.NewGuid(); var metadata = new KnowledgeMetadata("text/plain", "text", "en");
        return new(id, Guid.NewGuid(), "safe", values.Select((value, index) => Item(value, metadata, index)).ToArray(), metadata, [], DateTimeOffset.UtcNow, "document");
    }
    private static KnowledgeItem Item(string content, KnowledgeMetadata metadata, int order = 0) => new(Guid.NewGuid(), KnowledgeItemType.Text, content, order, new(Guid.NewGuid(), "safe", "display"), metadata, content);
    private static MemoryItem MemoryItem(string content, KnowledgeMetadata metadata) => new(Guid.NewGuid(), Guid.NewGuid(), KnowledgeItemType.Text, content, new(Guid.NewGuid(), "safe", "display"), metadata, content);

    private sealed class Generator(bool fail = false, string model = "model") : IEmbeddingGenerator
    {
        public EmbeddingModelInfo ModelInfo { get; } = new("fake", model, "1", 2, null, true);
        public int BatchCalls { get; private set; }
        public Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default) => Task.FromResult(Result(input));
        public Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return Task.FromResult(fail ? new EmbeddingBatchResult(EmbeddingStatus.Failed) : new EmbeddingBatchResult(EmbeddingStatus.Success, request.Inputs!.Select(Result).ToArray()));
        }
        private EmbeddingResult Result(EmbeddingInput input) => new(EmbeddingStatus.Success, new EmbeddingVector(input.Id, [1f, 0f], 2, ModelInfo.Model, ModelInfo.Version, input.ContentHash!, DateTimeOffset.UtcNow));
    }
}
