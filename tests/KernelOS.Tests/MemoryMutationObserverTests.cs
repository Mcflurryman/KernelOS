using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Memory;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class MemoryMutationObserverTests
{
    [Fact]
    public async Task CreateUpdateAndDeleteEmitIndependentCommittedSnapshots()
    {
        var observer = new RecordingObserver();
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptionsValue()), observer);
        var created = (await store.StoreAsync(new(Document("first")))).Document!;
        var changed = created.Items[0] with { Content = "second", ContentHash = "hash-second" };
        var updated = (await store.UpdateAsync(new(created.Id.ToString(), [changed], created.Metadata))).Document!;
        await store.DeleteAsync(new(created.Id.ToString()));

        Assert.Collection(observer.Mutations,
            mutation => { Assert.Equal(MemoryMutationType.Created, mutation.Type); Assert.Null(mutation.Previous); Assert.Equal(created.ContentHash, mutation.Current!.ContentHash); },
            mutation => { Assert.Equal(MemoryMutationType.Updated, mutation.Type); Assert.Equal("first", mutation.Previous!.Items[0].Content); Assert.Equal("second", mutation.Current!.Items[0].Content); },
            mutation => { Assert.Equal(MemoryMutationType.Deleted, mutation.Type); Assert.Equal("second", mutation.Previous!.Items[0].Content); Assert.Null(mutation.Current); });

        Assert.NotSame(updated.Items, observer.Mutations[1].Current!.Items);
        Assert.Equal(updated.ContentHash, observer.Mutations[2].Previous!.ContentHash);
    }

    [Fact]
    public async Task FailedObserverDoesNotChangeDurableMemorySuccessOrEmitForUncommittedRequests()
    {
        using var store = new InMemoryMemoryStore(Options.Create(MemoryOptionsValue()), new ThrowingObserver());
        var document = Document("content");

        var created = await store.StoreAsync(new(document));
        var duplicate = await store.StoreAsync(new(document));
        var missing = await store.UpdateAsync(new(Guid.NewGuid().ToString(), [], document.Metadata));

        Assert.Equal(MemoryStatus.Success, created.Status);
        Assert.Equal(MemoryStatus.AlreadyExists, duplicate.Status);
        Assert.Equal(MemoryStatus.NotFound, missing.Status);
        Assert.Equal(MemoryStatus.Success, (await store.GetAsync(created.Document!.Id.ToString())).Status);
    }

    private static MemoryOptions MemoryOptionsValue() => new() { MaxDocuments = 10, MaxItemsPerDocument = 10, MaxQueryResults = 10 };
    private static KnowledgeDocument Document(string content)
    {
        var id = Guid.NewGuid();
        var metadata = new KnowledgeMetadata("text/plain", "text", "en", new Dictionary<string, string> { ["safe"] = "value" });
        var item = new KnowledgeItem(Guid.NewGuid(), KnowledgeItemType.Text, content, 0, new(id, "safe", "display"), metadata, "hash-" + content);
        return new(id, Guid.NewGuid(), "title", [item], metadata, [], DateTimeOffset.UtcNow, "doc-" + content);
    }

    private sealed class RecordingObserver : IMemoryMutationObserver
    {
        public List<MemoryMutationCommitted> Mutations { get; } = [];
        public Task ObserveAsync(MemoryMutationCommitted mutation, CancellationToken cancellationToken = default) { Mutations.Add(mutation); return Task.CompletedTask; }
    }

    private sealed class ThrowingObserver : IMemoryMutationObserver
    {
        public Task ObserveAsync(MemoryMutationCommitted mutation, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
