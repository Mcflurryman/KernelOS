using KernelOS.Core.Embeddings;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.VectorIndex;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class VectorFamilyPatchTests
{
    [Fact]
    public async Task PatchIsAtomicIdempotentAndPreservesOtherFamilies()
    {
        using var index = Create();
        var family = new VectorFamilyKey("provider", "model", "1", 2);
        var other = Record(Guid.NewGuid(), "other");
        var original = Record(Guid.NewGuid(), "model");
        await index.AddAsync(new(other));
        await index.AddAsync(new(original));
        var replacement = Record(original.Embedding.InputId, "model") with { Id = Guid.NewGuid() };

        var patch = new VectorFamilyPatchRequest(family, [original.Id, Guid.NewGuid()], [replacement]);
        Assert.Equal(VectorIndexStatus.Success, (await index.ApplyFamilyPatchAsync(patch)).Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.ApplyFamilyPatchAsync(patch)).Status);

        var records = (await index.QueryAsync(new(Limit: 10))).Records!;
        Assert.Contains(records, record => record.Id == other.Id);
        Assert.Single(records.Where(record => VectorFamilyKey.From(record).Equals(family)));
        Assert.Equal(replacement.Embedding.InputId, records.Single(record => VectorFamilyKey.From(record).Equals(family)).Embedding.InputId);
    }

    [Fact]
    public async Task InvalidPatchPreservesExistingFamily()
    {
        using var index = Create();
        var record = Record(Guid.NewGuid(), "model");
        await index.AddAsync(new(record));
        var family = VectorFamilyKey.From(record);
        var wrongFamily = Record(Guid.NewGuid(), "other");

        var result = await index.ApplyFamilyPatchAsync(new(family, [record.Id], [wrongFamily]));

        Assert.Equal(VectorIndexStatus.InvalidRequest, result.Status);
        Assert.Equal(VectorIndexStatus.Success, (await index.GetAsync(record.Id)).Status);
    }

    [Fact]
    public async Task PatchRejectsDuplicateIdentityAndHonorsFinalLimit()
    {
        using var index = Create(maxRecords: 1);
        var first = Record(Guid.NewGuid(), "model");
        var duplicate = Record(first.Embedding.InputId, "model") with { Id = Guid.NewGuid() };
        var family = VectorFamilyKey.From(first);

        var duplicateResult = await index.ApplyFamilyPatchAsync(new(family, [], [first, duplicate]));
        var limitResult = await index.ApplyFamilyPatchAsync(new(family, [], [first, Record(Guid.NewGuid(), "model")]));

        Assert.Equal(VectorIndexStatus.InvalidRequest, duplicateResult.Status);
        Assert.Equal(VectorIndexStatus.TooLarge, limitResult.Status);
        Assert.Equal(0, await index.CountAsync());
    }

    private static InMemoryVectorIndex Create(int maxRecords = 100) => new(Options.Create(new VectorIndexOptions { MaxRecords = maxRecords, MaxQueryResults = 100, MaxMetadataEntries = 10 }));
    private static VectorRecord Record(Guid inputId, string model)
    {
        var vector = new EmbeddingVector(inputId, [1f, 0f], 2, model, "1", "HASH", DateTimeOffset.UtcNow);
        return new(Guid.NewGuid(), "provider", vector, Guid.NewGuid(), Guid.NewGuid(), inputId, Guid.NewGuid(), "HASH", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }
}
