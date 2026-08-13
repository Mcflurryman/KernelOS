using KernelOS.Core.Embeddings;
using KernelOS.Core.SemanticSearch;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.SemanticSearch;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class SemanticSearchCoreTests
{
    [Fact]
    public async Task SearchRanksCosineScoresAndFiltersIncompatibleVectors()
    {
        var index = new StubIndex(Record([1, 0]), Record([0, 1]), Record([1, 0], model: "other")); var engine = Engine(index);
        var result = await engine.SearchAsync(new(Vector([1, 0]), "ollama", TopK: 2));
        Assert.Equal(SemanticSearchStatus.Success, result.Status); Assert.Equal(2, result.Results!.Count); Assert.Equal(1f, result.Results[0].Score); Assert.Equal(.5f, result.Results[1].Score);
    }

    [Fact]
    public async Task SearchAppliesMinimumScoreTopKAndStableTieOrder()
    {
        var first = Record([1, 0]); var second = Record([1, 0]); var engine = Engine(new StubIndex(second, first));
        var result = await engine.SearchAsync(new(Vector([1, 0]), "ollama", TopK: 1, MinimumScore: .9f));
        Assert.NotNull(result.Results); var results = result.Results!; Assert.Single(results); Assert.Equal(new[] { first.Id, second.Id }.Min(), results[0].VectorRecordId);
    }

    [Fact]
    public async Task SearchValidatesCancellationAndEmptyIndex()
    {
        var engine = Engine(new StubIndex());
        Assert.Empty((await engine.SearchAsync(new(Vector([1, 0]), "ollama"))).Results!);
        Assert.Equal(SemanticSearchStatus.Cancelled, (await engine.SearchAsync(new(Vector([1, 0]), "ollama"), new(true))).Status);
        Assert.Equal(SemanticSearchStatus.InvalidRequest, (await engine.SearchAsync(new(Vector([1, 0]), "ollama", MinimumScore: 2))).Status);
    }

    [Theory]
    [InlineData(1f, 0f, 1f, 0f, 1f)]
    [InlineData(1f, 0f, 0f, 1f, .5f)]
    [InlineData(1f, 0f, -1f, 0f, 0f)]
    [InlineData(2f, 0f, 10f, 0f, 1f)]
    public async Task SearchMapsCosineToPublicScore(float ax, float ay, float bx, float by, float expected)
    {
        var engine = Engine(new StubIndex(Record([bx, by])));
        var result = await engine.SearchAsync(new(Vector([ax, ay]), "ollama"));
        Assert.Equal(expected, result.Results![0].Score, 5); Assert.All(result.Results, item => Assert.True(float.IsFinite(item.Score)));
    }

    [Fact]
    public async Task SearchSkipsZeroCandidateAndRejectsZeroQuery()
    {
        var engine = Engine(new StubIndex(Record([0, 0]), Record([1, 0])));
        Assert.Single((await engine.SearchAsync(new(Vector([1, 0]), "ollama"))).Results!);
        Assert.Equal(SemanticSearchStatus.InvalidRequest, (await engine.SearchAsync(new(Vector([0, 0]), "ollama"))).Status);
    }

    [Fact]
    public async Task SearchPagesUntilItFindsBestCandidate()
    {
        var records = new[] { Record([0, 1]), Record([0, 1]), Record([0, 1]), Record([0, 1]), Record([1, 0]) };
        var engine = Engine(new StubIndex(records), pageSize: 2);
        var result = await engine.SearchAsync(new(Vector([1, 0]), "ollama", TopK: 1));
        Assert.Equal(records[4].Id, result.Results![0].VectorRecordId);
    }

    [Fact]
    public async Task SearchReportsCandidateLimitAndValidatesTopKAndThreshold()
    {
        var engine = Engine(new StubIndex(Record([1, 0]), Record([0, 1]), Record([1, 0])), maxCandidates: 2, maxTopK: 2);
        var limited = await engine.SearchAsync(new(Vector([1, 0]), "ollama", TopK: 2));
        Assert.Equal(SemanticSearchStatus.PartialSuccess, limited.Status); Assert.Equal("SEMANTIC_CANDIDATE_LIMIT", limited.Warnings![0].Code);
        Assert.Equal(SemanticSearchStatus.InvalidRequest, (await engine.SearchAsync(new(Vector([1, 0]), "ollama", TopK: 3))).Status);
        Assert.Equal(SemanticSearchStatus.InvalidRequest, (await engine.SearchAsync(new(Vector([0, 1]), "ollama", MinimumScore: 1.1f))).Status);
    }

    private static EmbeddingVector Vector(float[] values, string model = "model") => new(Guid.NewGuid(), values, values.Length, model, "1", "HASH", DateTimeOffset.UtcNow);
    private static SemanticSearchEngine Engine(IVectorIndex index, int pageSize = 500, int maxCandidates = 10000, int maxTopK = 100) => new(index, Options.Create(new SemanticSearchOptions { CandidatePageSize = pageSize, MaxCandidates = maxCandidates, MaxTopK = maxTopK }));
    private static VectorRecord Record(float[] values, string model = "model") { var vector = Vector(values, model); return new(Guid.NewGuid(), "ollama", vector, null, null, null, null, vector.ContentHash, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow); }
    private sealed class StubIndex(params VectorRecord[] records) : IVectorIndex
    {
        public Task<VectorQueryResult> QueryAsync(VectorQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new VectorQueryResult(VectorIndexStatus.Success, records.Skip(query.Offset).Take(query.Limit).ToArray()));
        public Task<VectorAddResult> AddAsync(VectorAddRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<VectorUpdateResult> UpdateAsync(VectorUpdateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<VectorReplaceResult> ReplaceFamilyAsync(VectorReplaceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<VectorPatchResult> ApplyFamilyPatchAsync(VectorFamilyPatchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<VectorGetResult> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<bool> ContainsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<long> CountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
