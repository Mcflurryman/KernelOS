using System.Security.Cryptography;
using System.Text;
using KernelOS.Core.Embeddings;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Core.SemanticSearch;
using KernelOS.Core.VectorIndex;
using KernelOS.Core.VectorReindex;
using KernelOS.Infrastructure.Memory;
using KernelOS.Infrastructure.SemanticSearch;
using KernelOS.Infrastructure.VectorIndex;
using KernelOS.Infrastructure.VectorReindex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class MemoryVectorReindexServiceTests
{
    [Fact]
    public async Task ReindexBuildsDeterministicRecordsAndSupportsSemanticSearch()
    {
        using var store = Store(); using var index = Index();
        var item = (await store.StoreAsync(new(Document("alpha")))).Document!.Items.Single();
        var generator = new FakeGenerator();
        using var service = Service(store, index, generator);

        var result = await service.ReindexAsync();
        var query = await generator.GenerateAsync(new(Guid.NewGuid(), "alpha"));
        var semantic = new SemanticSearchEngine(index, Options.Create(new SemanticSearchOptions())).SearchAsync(new(query.Vector, generator.ModelInfo.Provider));

        Assert.Equal(VectorReindexStatus.Success, result.Status);
        Assert.True(result.Published); Assert.Equal(1, result.IndexedItems);
        Assert.Equal(item.Id, (await semantic).Results!.Single().Reference.MemoryItemId);
    }

    [Fact]
    public async Task DurableMemoryCanRebuildANewVectorIndexAfterRestart()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var stored = (await fixture.Store.StoreAsync(new(Document("durable")))).Document!;
        using var restartedIndex = Index(); var generator = new FakeGenerator();
        using var service = Service(fixture.Store, restartedIndex, generator);

        var result = await service.ReindexAsync();
        var records = await restartedIndex.QueryAsync(new(MemoryItemId: stored.Items.Single().Id, Limit: 10));

        Assert.Equal(VectorReindexStatus.Success, result.Status);
        Assert.Single(records.Records!);
    }

    [Fact]
    public async Task EmptyMemoryClearsActiveFamilyAndReportsNoMemory()
    {
        using var store = Store(); using var index = Index(); var generator = new FakeGenerator();
        var stale = Record(generator, Guid.NewGuid()); await index.AddAsync(new(stale));
        using var service = Service(store, index, generator);

        var result = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.NoMemory, result.Status); Assert.True(result.Published);
        Assert.Empty((await index.QueryAsync(new(Provider: generator.ModelInfo.Provider, Limit: 10))).Records!);
    }

    [Fact]
    public async Task DocumentsWithoutItemsClearActiveFamilyAndReportNoMemory()
    {
        using var index = Index(); var generator = new FakeGenerator();
        var stale = Record(generator, Guid.NewGuid()); await index.AddAsync(new(stale));
        var emptyDocument = new MemoryDocument(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new(1, DateTimeOffset.UtcNow, "hash"), [], new("text/plain", "text", "en"), "hash");
        using var service = Service(new SnapshotProvider(new MemorySnapshot([emptyDocument], DateTimeOffset.UtcNow)), index, generator);

        var result = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.NoMemory, result.Status); Assert.True(result.Published);
        Assert.Empty((await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 10))).Records!);
    }

    [Fact]
    public async Task DuplicateMemoryItemIdsFailBeforeEmbeddingOrPublication()
    {
        using var index = Index(); var generator = new FakeGenerator();
        var id = Guid.NewGuid(); var metadata = new KnowledgeMetadata("text/plain", "text", "en");
        var first = new MemoryItem(id, Guid.NewGuid(), KnowledgeItemType.Text, "first", new(Guid.NewGuid(), "safe", "display"), metadata, "first-hash");
        var second = first with { Content = "second", ContentHash = "second-hash" };
        var document = new MemoryDocument(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new(1, DateTimeOffset.UtcNow, "hash"), [first, second], metadata, "hash");
        using var service = Service(new SnapshotProvider(new MemorySnapshot([document], DateTimeOffset.UtcNow)), index, generator);

        var result = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.Failed, result.Status); Assert.Equal("MEMORY_ITEM_ID_DUPLICATE", result.ErrorCode);
        Assert.Equal(0, generator.BatchCalls); Assert.Equal(0, await index.CountAsync());
    }

    [Fact]
    public async Task CanonicalFamilyEncodingKeepsFormerlyAmbiguousFamiliesDistinct()
    {
        using var store = Store(); using var index = Index(); await store.StoreAsync(new(Document("value")));
        var firstGenerator = new FakeGenerator(model: "c", provider: "a|b");
        var secondGenerator = new FakeGenerator(model: "b|c", provider: "a");
        using var first = Service(store, index, firstGenerator); using var second = Service(store, index, secondGenerator);

        await first.ReindexAsync(); await second.ReindexAsync();
        var records = await index.QueryAsync(new(Limit: 10));

        Assert.Equal(2, records.Records!.Count);
        Assert.NotEqual(records.Records[0].Id, records.Records[1].Id);
    }

    [Fact]
    public async Task SameFamilyRebuildRemovesDeletedItemsAndPreservesOtherFamilies()
    {
        using var store = Store(); using var index = Index(); var generator = new FakeGenerator();
        var created = (await store.StoreAsync(new(Document("old")))).Document!;
        var other = Record(new FakeGenerator(model: "other"), Guid.NewGuid()); await index.AddAsync(new(other));
        using var service = Service(store, index, generator);
        await service.ReindexAsync();
        await store.UpdateAsync(new(created.Id.ToString(), [], created.Metadata));

        var result = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.NoMemory, result.Status);
        Assert.Empty((await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 10))).Records!);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(other.Id)).Status);
    }

    [Fact]
    public async Task ReindexIsIdempotentAndUsesBatches()
    {
        using var store = Store(maxDocuments: 120); using var index = Index(); var generator = new FakeGenerator();
        foreach (var number in Enumerable.Range(0, 100)) await store.StoreAsync(new(Document($"item-{number}")));
        using var service = Service(store, index, generator);

        var first = await service.ReindexAsync();
        var ids = (await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 200))).Records!.Select(record => record.Id).Order().ToArray();
        Assert.Equal(4, generator.BatchCalls);
        var second = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.Success, first.Status); Assert.Equal(VectorReindexStatus.Success, second.Status);
        Assert.Equal(8, generator.BatchCalls); Assert.Equal(100, await index.CountAsync());
        Assert.Equal(ids, (await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 200))).Records!.Select(record => record.Id).Order());
    }

    [Fact]
    public async Task FailedSecondBatchAndInvalidOutputPreserveOldFamily()
    {
        using var store = Store(); using var index = Index();
        var document = Document(Enumerable.Range(0, 33).Select(value => $"value-{value}").ToArray());
        await store.StoreAsync(new(document));
        var generator = new FakeGenerator(failBatch: 2); var old = Record(generator, Guid.NewGuid()); await index.AddAsync(new(old));
        using var service = Service(store, index, generator);

        var result = await service.ReindexAsync();

        Assert.Equal(VectorReindexStatus.Failed, result.Status); Assert.False(result.Published);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(old.Id)).Status);
    }

    [Fact]
    public async Task InvalidEmbeddingAndIndexLimitPreserveOldFamily()
    {
        using var store = Store(); await store.StoreAsync(new(Document("one", "two")));
        using var invalidIndex = Index(); var invalidGenerator = new FakeGenerator(invalidModel: true); var old = Record(new FakeGenerator(), Guid.NewGuid()); await invalidIndex.AddAsync(new(old));
        using var invalidService = Service(store, invalidIndex, invalidGenerator);

        var invalid = await invalidService.ReindexAsync();

        Assert.Equal(VectorReindexStatus.Failed, invalid.Status); Assert.Equal(VectorIndexStatus.Success, (await invalidIndex.GetAsync(old.Id)).Status);
        using var limitedIndex = Index(maxRecords: 1); var limitGenerator = new FakeGenerator(); var limitedOld = Record(limitGenerator, Guid.NewGuid()); await limitedIndex.AddAsync(new(limitedOld));
        using var limitedService = Service(store, limitedIndex, limitGenerator);
        var limited = await limitedService.ReindexAsync();
        Assert.Equal(VectorReindexStatus.Failed, limited.Status); Assert.Equal(VectorIndexStatus.Success, (await limitedIndex.GetAsync(limitedOld.Id)).Status);
    }

    [Fact]
    public async Task UpdatedContentReplacesSameMemoryItemVector()
    {
        using var store = Store(); using var index = Index(); var generator = new FakeGenerator();
        var created = (await store.StoreAsync(new(Document("old")))).Document!; using var service = Service(store, index, generator);
        await service.ReindexAsync();
        var oldVector = (await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 10))).Records!.Single();
        var changed = created.Items.Single() with { Content = "new", ContentHash = EmbeddingText.Hash("new") };
        await store.UpdateAsync(new(created.Id.ToString(), [changed], created.Metadata));

        await service.ReindexAsync();
        var replacement = (await index.QueryAsync(new(Model: generator.ModelInfo.Model, Limit: 10))).Records!.Single();

        Assert.Equal(changed.Id, replacement.Embedding.InputId); Assert.NotEqual(oldVector.ContentHash, replacement.ContentHash);
    }

    [Fact]
    public async Task DeletedDocumentAndModelFamilyChangeReconcileOnlyTheActiveFamily()
    {
        using var store = Store(); using var index = Index();
        var first = (await store.StoreAsync(new(Document("first")))).Document!;
        var second = (await store.StoreAsync(new(Document("second")))).Document!;
        var familyA = new FakeGenerator(model: "a"); using var reindexA = Service(store, index, familyA);
        await reindexA.ReindexAsync();
        await store.DeleteAsync(new(first.Id.ToString()));
        var familyB = new FakeGenerator(model: "b"); using var reindexB = Service(store, index, familyB);

        await reindexB.ReindexAsync();
        await reindexA.ReindexAsync();

        Assert.Single((await index.QueryAsync(new(Model: "a", Limit: 10))).Records!);
        Assert.Single((await index.QueryAsync(new(Model: "b", Limit: 10))).Records!);
        Assert.Equal(second.Items.Single().Id, (await index.QueryAsync(new(Model: "a", Limit: 10))).Records!.Single().MemoryItemId);
    }

    [Fact]
    public async Task CancellationAndConcurrentCallsPreserveOldFamily()
    {
        using var store = Store(); using var index = Index(); await store.StoreAsync(new(Document("value")));
        var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var generator = new FakeGenerator(entered: entered, release: release); var old = Record(generator, Guid.NewGuid()); await index.AddAsync(new(old));
        using var service = Service(store, index, generator);
        var first = service.ReindexAsync(); await entered.Task;
        var second = await service.ReindexAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = await service.ReindexAsync(cancellation.Token);
        release.SetResult();

        Assert.Equal(VectorReindexStatus.AlreadyRunning, second.Status);
        Assert.Equal(VectorReindexStatus.Cancelled, cancelled.Status);
        Assert.Equal(VectorReindexStatus.Success, (await first).Status);
    }

    [Fact]
    public async Task CancellationDuringEmbeddingPreservesOldFamily()
    {
        using var store = Store(); using var index = Index(); await store.StoreAsync(new(Document("value")));
        var entered = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var generator = new FakeGenerator(entered: entered, release: release); var old = Record(generator, Guid.NewGuid()); await index.AddAsync(new(old));
        using var service = Service(store, index, generator); using var cancellation = new CancellationTokenSource();
        var reindex = service.ReindexAsync(cancellation.Token); await entered.Task;
        cancellation.Cancel();

        var result = await reindex;

        Assert.Equal(VectorReindexStatus.Cancelled, result.Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(old.Id)).Status);
    }

    private static InMemoryMemoryStore Store(int maxDocuments = 20) => new(Options.Create(new MemoryOptions { MaxDocuments = maxDocuments, MaxItemsPerDocument = 200, MaxQueryResults = 200 }));
    private static InMemoryVectorIndex Index(int maxRecords = 200) => new(Options.Create(new VectorIndexOptions { MaxRecords = maxRecords, MaxQueryResults = 200, MaxMetadataEntries = 10 }));
    private static MemoryVectorReindexService Service(IMemorySnapshotProvider store, IVectorIndex index, params IEmbeddingGenerator[] generators) => new(store, index, generators, TimeProvider.System, NullLogger<MemoryVectorReindexService>.Instance);
    private static KnowledgeDocument Document(params string[] values)
    {
        var metadata = new KnowledgeMetadata("text/plain", "text", "en");
        return new(Guid.NewGuid(), Guid.NewGuid(), "document", values.Select((value, ordinal) => new KnowledgeItem(Guid.NewGuid(), KnowledgeItemType.Text, value, ordinal, new(Guid.NewGuid(), "safe", "display"), metadata, EmbeddingText.Hash(value))).ToArray(), metadata, [], DateTimeOffset.UtcNow, "hash");
    }
    private static VectorRecord Record(FakeGenerator generator, Guid inputId)
    {
        var vector = generator.GenerateAsync(new(inputId, "stale", "stale-hash")).Result.Vector!;
        return new(Guid.NewGuid(), generator.ModelInfo.Provider, vector, null, null, null, null, vector.ContentHash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private sealed class FakeGenerator : IEmbeddingGenerator
    {
        private readonly int? failBatch; private readonly TaskCompletionSource? entered; private readonly TaskCompletionSource? release; private readonly bool invalidModel;
        public FakeGenerator(int? failBatch = null, TaskCompletionSource? entered = null, TaskCompletionSource? release = null, string model = "model", bool invalidModel = false, string provider = "fake") { this.failBatch = failBatch; this.entered = entered; this.release = release; this.invalidModel = invalidModel; ModelInfo = new(provider, model, "1", 3, null, true); }
        public EmbeddingModelInfo ModelInfo { get; }
        public int BatchCalls { get; private set; }
        public Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default) => Task.FromResult(Result(input));
        public async Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default)
        {
            BatchCalls++; entered?.TrySetResult(); if (release is not null) await release.Task.WaitAsync(cancellationToken);
            if (failBatch == BatchCalls) return new(EmbeddingStatus.Failed);
            var results = request.Inputs!.Select(Result).ToArray(); return new(EmbeddingStatus.Success, results);
        }
        private EmbeddingResult Result(EmbeddingInput input)
        {
            var hash = input.ContentHash ?? EmbeddingText.Hash(EmbeddingText.Normalize(input.Text!));
            var values = Enumerable.Range(0, 3).Select(index => BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{hash}:{index}")), 0) / (float)uint.MaxValue);
            return new(EmbeddingStatus.Success, new EmbeddingVector(input.Id, values, 3, invalidModel ? "wrong" : ModelInfo.Model, ModelInfo.Version, hash, DateTimeOffset.UtcNow));
        }
    }

    private sealed class SnapshotProvider(MemorySnapshot snapshot) : IMemorySnapshotProvider
    {
        public Task<MemorySnapshotResult> CreateSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(cancellationToken.IsCancellationRequested ? new MemorySnapshotResult(MemoryStatus.Cancelled) : new MemorySnapshotResult(MemoryStatus.Success, snapshot));
    }
}
