using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Memory;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class MemoryStoreContractTests
{
    [Fact]
    public async Task StoresHaveParityForCreateGetUpdateDeleteAndCancellation()
    {
        foreach (var create in Factories())
        {
            await using var scope = await create();
            var source = Document("one", KnowledgeItemType.Text, "hash-one", "author", "Kai");
            var created = await scope.Store.StoreAsync(new(source));
            Assert.Equal(MemoryStatus.Success, created.Status);
            Assert.Equal(1, created.Document!.Version.Number);
            Assert.Equal(MemoryStatus.AlreadyExists, (await scope.Store.StoreAsync(new(source))).Status);
            var fetched = await scope.Store.GetAsync(created.Document.Id.ToString("D"));
            AssertEquivalent(created.Document, fetched.Document!);

            var replacementMetadata = new KnowledgeMetadata("text/markdown", "markdown", "en", new Dictionary<string, string> { ["author"] = "Updated" });
            var replacement = created.Document.Items[0] with { Content = "updated", ContentHash = "hash-updated", Metadata = replacementMetadata };
            var updated = await scope.Store.UpdateAsync(new(created.Document.Id.ToString("D"), [replacement], replacementMetadata));
            Assert.Equal(MemoryStatus.Success, updated.Status);
            Assert.Equal(2, updated.Document!.Version.Number);
            Assert.Equal(created.Document.CreatedAt, updated.Document.CreatedAt);
            Assert.Equal("updated", updated.Document.Items.Single().Content);
            Assert.Equal(MemoryStatus.NotFound, (await scope.Store.UpdateAsync(new(Guid.NewGuid().ToString("D"), [replacement], replacementMetadata))).Status);
            Assert.Equal(MemoryStatus.InvalidRequest, (await scope.Store.UpdateAsync(new("invalid", [replacement], replacementMetadata))).Status);
            Assert.Equal(MemoryStatus.Success, (await scope.Store.DeleteAsync(new(created.Document.Id.ToString("D")))).Status);
            Assert.Equal(MemoryStatus.NotFound, (await scope.Store.GetAsync(created.Document.Id.ToString("D"))).Status);
            Assert.Equal(MemoryStatus.NotFound, (await scope.Store.DeleteAsync(new(created.Document.Id.ToString("D")))).Status);
            Assert.Equal(MemoryStatus.InvalidRequest, (await scope.Store.DeleteAsync(new("invalid"))).Status);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Store.StoreAsync(new(Document("cancel", KnowledgeItemType.Text, "h", "a", "b")), cancelled.Token)).Status);
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Store.GetAsync(Guid.NewGuid().ToString("D"), cancelled.Token)).Status);
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Store.UpdateAsync(new(Guid.NewGuid().ToString("D"), [], replacementMetadata), cancelled.Token)).Status);
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Store.DeleteAsync(new(Guid.NewGuid().ToString("D")), cancelled.Token)).Status);
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Store.QueryAsync(new(), cancelled.Token)).Status);
        }
    }

    [Fact]
    public async Task StoresHaveParityForEveryQueryFilterAndSnapshots()
    {
        foreach (var create in Factories())
        {
            await using var scope = await create();
            var first = (await scope.Store.StoreAsync(new(Document("alpha", KnowledgeItemType.Text, "hash-a", "author", "Kai")))).Document!;
            var second = (await scope.Store.StoreAsync(new(Document("beta", KnowledgeItemType.Code, "hash-b", "author", "Other")))).Document!;
            var queries = new[]
            {
                new MemoryQuery(Id: first.Id.ToString("D").ToUpperInvariant()), new MemoryQuery(KnowledgeDocumentId: second.KnowledgeDocumentId),
                new MemoryQuery(MemoryItemId: first.Items[0].Id), new MemoryQuery(ItemType: KnowledgeItemType.Code),
                new MemoryQuery(ExactContent: "alpha"), new MemoryQuery(ContentHash: "hash-b"), new MemoryQuery(MetadataKey: "author", MetadataValue: "Kai"),
                new MemoryQuery(ItemType: KnowledgeItemType.Code, MetadataKey: "author", MetadataValue: "Kai"), new MemoryQuery(ExactContent: "ALPHA"),
                new MemoryQuery(ExactContent: "' OR 1=1 --"), new MemoryQuery(MetadataKey: "author", MetadataValue: "\"; DROP TABLE memory_documents; --")
            };
            foreach (var query in queries) Assert.Equal(MemoryStatus.Success, (await scope.Store.QueryAsync(query)).Status);
            Assert.Equal([first.Id], (await scope.Store.QueryAsync(queries[0])).Documents!.Select(document => document.Id));
            Assert.Equal([second.Id], (await scope.Store.QueryAsync(queries[1])).Documents!.Select(document => document.Id));
            Assert.Equal([first.Id], (await scope.Store.QueryAsync(queries[2])).Documents!.Select(document => document.Id));
            Assert.Equal([second.Id], (await scope.Store.QueryAsync(queries[3])).Documents!.Select(document => document.Id));
            Assert.Empty((await scope.Store.QueryAsync(queries[8])).Documents!);
            Assert.Empty((await scope.Store.QueryAsync(queries[9])).Documents!);
            Assert.Empty((await scope.Store.QueryAsync(queries[10])).Documents!);
            var paged = await scope.Store.QueryAsync(new(Limit: 10, Offset: 1));
            Assert.Single(paged.Documents!);
            Assert.Empty((await scope.Store.QueryAsync(new(Limit: 1, Offset: 10))).Documents!);
            var snapshot = (await scope.Store.QueryAsync(new(ExactContent: "alpha"))).Documents!.Single();
            Assert.IsType<Dictionary<string, string>>(snapshot.Metadata.Properties)["author"] = "mutated";
            Assert.Equal("Kai", (await scope.Store.GetAsync(first.Id.ToString("D"))).Document!.Metadata.Properties!["author"]);
        }
    }

    [Fact]
    public async Task SnapshotProvidersReturnCompleteOrderedAndIsolatedMaterializedViews()
    {
        foreach (var create in Factories())
        {
            await using var scope = await create();
            var first = (await scope.Store.StoreAsync(new(Document("first", KnowledgeItemType.Text, "first-hash", "author", "First")))).Document!;
            var second = (await scope.Store.StoreAsync(new(Document("second", KnowledgeItemType.Code, "second-hash", "author", "Second")))).Document!;

            var result = await scope.Snapshots.CreateSnapshotAsync();

            Assert.Equal(MemoryStatus.Success, result.Status);
            var snapshot = result.Snapshot!;
            Assert.Equal(2, snapshot.TotalDocuments);
            Assert.Equal(2, snapshot.TotalItems);
            Assert.Equal(snapshot.Documents.OrderByDescending(document => document.UpdatedAt).ThenBy(document => document.Id).Select(document => document.Id), snapshot.Documents.Select(document => document.Id));
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<MemoryDocument>>(snapshot.Documents);
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyDictionary<string, string>>(snapshot.Documents[0].Metadata.Properties);
            Assert.Equal("First", (await scope.Store.GetAsync(first.Id.ToString("D"))).Document!.Metadata.Properties!["author"]);

            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(MemoryStatus.Cancelled, (await scope.Snapshots.CreateSnapshotAsync(cancelled.Token)).Status);
        }
    }

    private static IEnumerable<Func<Task<StoreScope>>> Factories()
    {
        yield return () => Task.FromResult<StoreScope>(new InMemoryScope());
        yield return SqliteScope.CreateAsync;
    }

    private static KnowledgeDocument Document(string content, KnowledgeItemType type, string hash, string key, string value)
    {
        var metadata = new KnowledgeMetadata("text/plain", "text", "en", new Dictionary<string, string> { [key] = value });
        return new(Guid.NewGuid(), Guid.NewGuid(), "document", [new KnowledgeItem(Guid.NewGuid(), type, content, 0, new(Guid.NewGuid(), "safe", "display", new(Line: 1)), metadata, hash)], metadata, [], DateTimeOffset.UtcNow, hash);
    }

    private static void AssertEquivalent(MemoryDocument expected, MemoryDocument actual)
    {
        Assert.Equal(expected.Id, actual.Id); Assert.Equal(expected.KnowledgeDocumentId, actual.KnowledgeDocumentId); Assert.Equal(expected.ContentHash, actual.ContentHash); Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Items.Select(item => item.Content), actual.Items.Select(item => item.Content)); Assert.Equal(expected.Metadata.Properties, actual.Metadata.Properties);
    }

    private abstract class StoreScope : IAsyncDisposable { internal abstract IMemoryStore Store { get; } internal abstract IMemorySnapshotProvider Snapshots { get; } public abstract ValueTask DisposeAsync(); }
    private sealed class InMemoryScope : StoreScope
    {
        private readonly InMemoryMemoryStore store = new(Options.Create(new MemoryOptions { MaxDocuments = 100, MaxItemsPerDocument = 100, MaxQueryResults = 2 }));
        internal override IMemoryStore Store => store;
        internal override IMemorySnapshotProvider Snapshots => store;
        public override ValueTask DisposeAsync() { store.Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class SqliteScope(SqliteMemoryFixture fixture) : StoreScope
    {
        internal override IMemoryStore Store => fixture.Store;
        internal override IMemorySnapshotProvider Snapshots => fixture.Store;
        internal static async Task<StoreScope> CreateAsync() => new SqliteScope(await SqliteMemoryFixture.CreateAsync());
        public override ValueTask DisposeAsync() => fixture.DisposeAsync();
    }
}
