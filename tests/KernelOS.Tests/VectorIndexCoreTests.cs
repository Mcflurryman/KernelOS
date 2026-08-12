using KernelOS.Core.Embeddings;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.VectorIndex;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class VectorIndexCoreTests
{
    [Fact]
    public async Task AddGetAndCountStoreSafeSnapshot()
    {
        var index = Create(); var metadata = new Dictionary<string, string> { ["kind"] = "note" }; var record = Record(metadata: metadata);
        var added = await index.AddAsync(new(record)); metadata["kind"] = "changed";
        var loaded = await index.GetAsync(record.Id);
        Assert.Equal(VectorIndexStatus.Success, added.Status); Assert.Equal(VectorIndexStatus.Success, loaded.Status); Assert.Equal("note", loaded.Record!.Metadata!["kind"]); Assert.Equal(1, await index.CountAsync()); Assert.True(await index.ContainsAsync(record.Id));
    }

    [Fact]
    public async Task AddRejectsEmptyAndDuplicateIdentityButAllowsDifferentModel()
    {
        var index = Create(); var record = Record();
        Assert.Equal(VectorIndexStatus.InvalidRequest, (await index.AddAsync(new(record with { Id = Guid.Empty }))).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.AddAsync(new(record))).Status);
        Assert.Equal(VectorIndexStatus.AlreadyExists, (await index.AddAsync(new(record with { Id = Guid.NewGuid() }))).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.AddAsync(new(Record(inputId: record.Embedding.InputId, model: "other")))).Status);
    }

    [Fact]
    public async Task UpdatePreservesCreatedAtAndMaintainsIdentityIndex()
    {
        var index = Create(); var record = Record(); var added = await index.AddAsync(new(record));
        var updated = await index.UpdateAsync(new(record.Id, "ollama", Vector(record.Embedding.InputId, "new", 3), null, null, null, null, new Dictionary<string, string> { ["v"] = "2" }));
        Assert.Equal(VectorIndexStatus.Success, updated.Status); Assert.Equal(added.Record!.CreatedAt, updated.Record!.CreatedAt); Assert.Equal("new", updated.Record.Embedding.Model);
        Assert.Equal(VectorIndexStatus.Success, (await index.AddAsync(new(record with { Id = Guid.NewGuid() }))).Status);
    }

    [Fact]
    public async Task DeleteRemovesRecordAndSecondDeleteIsNotFound()
    {
        var index = Create(); var record = Record(); await index.AddAsync(new(record));
        Assert.Equal(VectorIndexStatus.Success, (await index.DeleteAsync(new(record.Id))).Status);
        Assert.Equal(VectorIndexStatus.NotFound, (await index.DeleteAsync(new(record.Id))).Status);
        Assert.False(await index.ContainsAsync(record.Id));
    }

    [Fact]
    public async Task QueryFiltersCompatibleFamilyAndReturnsCopiesInStableOrder()
    {
        var index = Create(); var input = Guid.NewGuid(); var first = Record(inputId: input, model: "m1"); var second = Record(model: "m2");
        await index.AddAsync(new(first)); await index.AddAsync(new(second));
        var result = await index.QueryAsync(new(Model: "m1", Dimensions: 3, Limit: 10));
        Assert.Equal(VectorIndexStatus.Success, result.Status); Assert.NotNull(result.Records); var records = result.Records!; Assert.Single(records); Assert.Equal(first.Id, records[0].Id);
    }

    [Fact]
    public async Task LimitsAndCancellationAreControlled()
    {
        var index = Create(maxRecords: 1, maxMetadata: 1); var token = new CancellationToken(true);
        Assert.Equal(VectorIndexStatus.Cancelled, (await index.AddAsync(new(Record()), token)).Status);
        Assert.Equal(VectorIndexStatus.InvalidRequest, (await index.AddAsync(new(Record(metadata: new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" })))).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.AddAsync(new(Record()))).Status);
        Assert.Equal(VectorIndexStatus.TooLarge, (await index.AddAsync(new(Record()))).Status);
        Assert.Equal(VectorIndexStatus.Cancelled, (await index.QueryAsync(new(), token)).Status);
    }

    [Fact]
    public async Task ConcurrentAddOfSameIdHasExactlyOneSuccess()
    {
        var index = Create(); var record = Record(); var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => index.AddAsync(new(record))));
        Assert.Single(results.Where(result => result.Status == VectorIndexStatus.Success)); Assert.Equal(1, await index.CountAsync());
    }

    [Fact]
    public async Task ReplaceFamilyPublishesNewFamilyAndPreservesOtherFamilies()
    {
        var index = Create();
        var old = Record(model: "active");
        var other = Record(model: "other");
        var replacement = Record(model: "active");
        await index.AddAsync(new(old));
        await index.AddAsync(new(other));

        var result = await index.ReplaceFamilyAsync(new(VectorFamilyKey.From(replacement), [replacement]));

        Assert.Equal(VectorIndexStatus.Success, result.Status);
        Assert.Equal(1, result.ReplacedCount);
        Assert.Equal(VectorIndexStatus.NotFound, (await index.GetAsync(old.Id)).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(replacement.Id)).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(other.Id)).Status);
        Assert.Equal(2, await index.CountAsync());
    }

    [Fact]
    public async Task ReplaceFamilyValidatesEverythingBeforePublishing()
    {
        var index = Create();
        var old = Record(); await index.AddAsync(new(old));
        var duplicateInput = Guid.NewGuid();
        var first = Record(inputId: duplicateInput);
        var second = Record(inputId: duplicateInput);

        var result = await index.ReplaceFamilyAsync(new(VectorFamilyKey.From(first), [first, second]));

        Assert.Equal(VectorIndexStatus.InvalidRequest, result.Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(old.Id)).Status);
        Assert.Equal(1, await index.CountAsync());

        var invalid = Record() with { ContentHash = "different" };
        var invalidResult = await index.ReplaceFamilyAsync(new(VectorFamilyKey.From(invalid), [invalid]));
        Assert.Equal(VectorIndexStatus.InvalidRequest, invalidResult.Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(old.Id)).Status);
    }

    [Fact]
    public async Task ReplaceFamilyWithEmptySetRemovesOnlyThatFamily()
    {
        var index = Create();
        var active = Record(model: "active"); var other = Record(model: "other");
        await index.AddAsync(new(active)); await index.AddAsync(new(other));

        var result = await index.ReplaceFamilyAsync(new(VectorFamilyKey.From(active), []));

        Assert.Equal(VectorIndexStatus.Success, result.Status);
        Assert.Equal(0, result.ReplacedCount);
        Assert.False(await index.ContainsAsync(active.Id));
        Assert.True(await index.ContainsAsync(other.Id));
        Assert.Single((await index.QueryAsync(new(Model: "other", Limit: 10))).Records!);
    }

    [Fact]
    public async Task ReplaceFamilyCancellationAndConcurrentReadersPreserveCompleteStates()
    {
        var index = Create();
        var old = Record(model: "active"); await index.AddAsync(new(old));
        var replacement = Record(model: "active");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();

        Assert.Equal(VectorIndexStatus.Cancelled, (await index.ReplaceFamilyAsync(new(VectorFamilyKey.From(replacement), [replacement]), cancelled.Token)).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(old.Id)).Status);

        var observations = new List<Guid>();
        var reader = Task.Run(async () =>
        {
            for (var indexValue = 0; indexValue < 100; indexValue++)
            {
                var query = await index.QueryAsync(new(Model: "active", Limit: 10));
                observations.AddRange(query.Records!.Select(record => record.Id));
            }
        });
        var replace = index.ReplaceFamilyAsync(new(VectorFamilyKey.From(replacement), [replacement]));
        await Task.WhenAll(reader, replace);
        var replaced = await replace;

        Assert.Equal(VectorIndexStatus.Success, replaced.Status);
        Assert.All(observations, observation => Assert.True(observation == old.Id || observation == replacement.Id));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("model")]
    [InlineData("version")]
    [InlineData("dimensions")]
    [InlineData("hash")]
    [InlineData("memory")]
    [InlineData("metadata")]
    public async Task QuerySupportsAdministrativeFilters(string filter)
    {
        var index = Create(); var memoryId = Guid.NewGuid(); var record = Record(metadata: new Dictionary<string, string> { ["scope"] = "test" }) with { MemoryDocumentId = memoryId };
        await index.AddAsync(new(record));
        var query = filter switch
        {
            "provider" => new VectorQuery(Provider: "ollama"), "model" => new VectorQuery(Model: "model"), "version" => new VectorQuery(Version: "1"),
            "dimensions" => new VectorQuery(Dimensions: 3), "hash" => new VectorQuery(ContentHash: record.ContentHash), "memory" => new VectorQuery(MemoryDocumentId: memoryId),
            _ => new VectorQuery(MetadataKey: "scope", MetadataValue: "test")
        };
        var result = await index.QueryAsync(query);
        Assert.Equal(VectorIndexStatus.Success, result.Status); Assert.Single(result.Records!);
    }

    private static InMemoryVectorIndex Create(int maxRecords = 100, int maxMetadata = 50) => new(Options.Create(new VectorIndexOptions { MaxRecords = maxRecords, MaxQueryResults = 20, MaxMetadataEntries = maxMetadata }));
    private static VectorRecord Record(Guid? inputId = null, string model = "model", IReadOnlyDictionary<string, string>? metadata = null) { var embedding = Vector(inputId ?? Guid.NewGuid(), model, 3); var now = DateTimeOffset.UtcNow; return new(Guid.NewGuid(), "ollama", embedding, null, null, null, null, embedding.ContentHash, now, now, metadata); }
    private static EmbeddingVector Vector(Guid inputId, string model, int dimensions) => new(inputId, Enumerable.Range(1, dimensions).Select(value => (float)value), dimensions, model, "1", EmbeddingText.Hash(model), DateTimeOffset.UtcNow);
}
