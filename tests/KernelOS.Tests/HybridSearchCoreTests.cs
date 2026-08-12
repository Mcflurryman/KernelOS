using KernelOS.Core.Embeddings;
using KernelOS.Core.HybridSearch;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Search;
using KernelOS.Core.SemanticSearch;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.HybridSearch;
using KernelOS.Infrastructure.SemanticSearch;
using KernelOS.Infrastructure.VectorIndex;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class HybridSearchCoreTests
{
    [Fact]
    public async Task MergesByMemoryItemAndNormalizesLexicalScores()
    {
        var item = Guid.NewGuid(); var engine = Engine([Hit(item, 100), Hit(Guid.NewGuid(), 50)], [Semantic(item, .8f)]);
        var result = await engine.SearchAsync(new("query", 10));
        Assert.Equal(HybridSearchStatus.Success, result.Status); Assert.Equal(2, result.Results!.Count); Assert.Equal(item, result.Results[0].MemoryId); Assert.Equal(1, result.Results[0].LexicalScore); Assert.Equal(.8f, result.Results[0].SemanticScore); Assert.Equal(.88f, result.Results[0].HybridScore, 3); Assert.Equal(.5f, result.Results[1].LexicalScore);
    }
    [Fact]
    public async Task DegradesToLexicalOnlyWhenNoEmbeddingProviderIsAvailable()
    {
        var id = Guid.NewGuid();
        var result = await Engine([Hit(id, 10)], [], generators: []).SearchAsync(new("q"));

        Assert.Equal(HybridSearchStatus.PartialSuccess, result.Status);
        Assert.Equal(1f, Assert.Single(result.Results!).HybridScore);
        Assert.Contains(result.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task DegradesToLexicalOnlyWhenEmbeddingProviderSelectionIsAmbiguous()
    {
        var result = await Engine([Hit(Guid.NewGuid(), 10)], [], generators: [new FakeGenerator(), new FakeGenerator()]).SearchAsync(new("q"));

        Assert.Equal(HybridSearchStatus.PartialSuccess, result.Status);
        Assert.Equal(1f, Assert.Single(result.Results!).HybridScore);
        Assert.Contains(result.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_PROVIDER_UNAVAILABLE");
    }
    [Theory]
    [InlineData(1f,1f,1f)] [InlineData(1f,0f,.4f)] [InlineData(0f,1f,.6f)]
    public async Task ComputesWeightedScores(float lexicalScore, float semanticScore, float expected)
    { var id=Guid.NewGuid(); var result=await Engine([Hit(id,(int)(lexicalScore*100))],[Semantic(id,semanticScore)]).SearchAsync(new("q")); Assert.Equal(expected,result.Results![0].HybridScore,3); }
    [Fact]
    public async Task DegradesAndOrdersDeterministically()
    { var a=Guid.NewGuid(); var b=Guid.NewGuid(); var partial=await Engine([Hit(a,10)],[],semanticStatus:SemanticSearchStatus.Failed).SearchAsync(new("q")); Assert.Equal(HybridSearchStatus.PartialSuccess,partial.Status); Assert.Equal("HYBRID_SEMANTIC_SEARCH_FAILED",partial.Warnings![0].Code); Assert.Equal(1f, Assert.Single(partial.Results!).HybridScore); var ranked=await Engine([Hit(b,10),Hit(a,10)],[]).SearchAsync(new("q")); Assert.Equal(new[]{a,b}.OrderBy(id=>id),ranked.Results!.Select(x=>x.MemoryId)); }

    [Fact]
    public async Task PreservesHealthyEmptyBranchWeightButRenormalizesTechnicalFallbacks()
    {
        var id = Guid.NewGuid();
        var healthyEmpty = await Engine([Hit(id, 10)], []).SearchAsync(new("q"));
        var embeddingFailed = await Engine([Hit(id, 10)], [], generators: [new FakeGenerator(EmbeddingStatus.Failed)]).SearchAsync(new("q"));
        var semanticOnly = await Engine(new FakeSearch([], SearchStatus.Failed), new FakeSemantic([Semantic(id, .8f)], SemanticSearchStatus.Success)).SearchAsync(new("q"));

        Assert.Equal(HybridSearchStatus.Success, healthyEmpty.Status);
        Assert.Equal(.4f, Assert.Single(healthyEmpty.Results!).HybridScore, 3);
        Assert.Equal(HybridSearchStatus.PartialSuccess, embeddingFailed.Status);
        Assert.Equal(1f, Assert.Single(embeddingFailed.Results!).HybridScore);
        Assert.Contains(embeddingFailed.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_EMBEDDING_FAILED");
        Assert.Equal(HybridSearchStatus.PartialSuccess, semanticOnly.Status);
        Assert.Equal(.8f, Assert.Single(semanticOnly.Results!).HybridScore, 3);
    }

    [Fact]
    public async Task PreservesLexicalResultsWhenEmbeddingOrSemanticThrows()
    {
        var id = Guid.NewGuid();
        var embeddingThrow = await Engine(new FakeSearch([Hit(id, 10)]), new FakeSemantic([], SemanticSearchStatus.Success), [new ThrowingGenerator()]).SearchAsync(new("q"));
        var semanticThrow = await Engine(new FakeSearch([Hit(id, 10)]), new ThrowingSemantic()).SearchAsync(new("q"));

        Assert.Equal(HybridSearchStatus.PartialSuccess, embeddingThrow.Status);
        Assert.Equal(1f, Assert.Single(embeddingThrow.Results!).HybridScore);
        Assert.Contains(embeddingThrow.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_EMBEDDING_FAILED");
        Assert.Equal(HybridSearchStatus.PartialSuccess, semanticThrow.Status);
        Assert.Equal(1f, Assert.Single(semanticThrow.Results!).HybridScore);
        Assert.Contains(semanticThrow.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_SEARCH_FAILED");
    }

    [Fact]
    public async Task PreservesSemanticResultsWhenLexicalThrowsAndCancellationStillPrecedesFallback()
    {
        var id = Guid.NewGuid();
        var semanticOnly = await Engine(new ThrowingSearch(), new FakeSemantic([Semantic(id, .8f)], SemanticSearchStatus.Success)).SearchAsync(new("q"));
        using var cancellation = new CancellationTokenSource();
        var generator = new FakeGenerator();
        var cancelled = await Engine(new CancellingSearch(cancellation), new FakeSemantic([], SemanticSearchStatus.Success), [generator]).SearchAsync(new("q"), cancellation.Token);

        Assert.Equal(HybridSearchStatus.PartialSuccess, semanticOnly.Status);
        Assert.Equal(.8f, Assert.Single(semanticOnly.Results!).HybridScore, 3);
        Assert.Contains(semanticOnly.Warnings!, warning => warning.Code == "HYBRID_LEXICAL_FAILED");
        Assert.Equal(HybridSearchStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, generator.Calls);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(10, 30)]
    [InlineData(50, 100)]
    public async Task UsesBoundedCandidateBudgetForBothSources(int topK, int expectedCandidates)
    {
        var lexical = new FakeSearch([]); var semantic = new FakeSemantic([], SemanticSearchStatus.Success);
        var engine = Engine(lexical, semantic);
        await engine.SearchAsync(new("query", topK));
        Assert.Equal(expectedCandidates, lexical.LastQuery!.Limit); Assert.Equal(expectedCandidates, semantic.LastRequest!.TopK);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task RejectsInvalidTopK(int topK) => Assert.Equal(HybridSearchStatus.InvalidRequest, (await Engine([], []).SearchAsync(new("query", topK))).Status);

    [Fact]
    public async Task ReturnsCancelledWithoutGeneratingEmbeddingWhenLexicalSearchIsCancelled()
    {
        var generator = new FakeGenerator(); var engine = Engine(new FakeSearch([], SearchStatus.Cancelled), new FakeSemantic([], SemanticSearchStatus.Success), [generator]);
        Assert.Equal(HybridSearchStatus.Cancelled, (await engine.SearchAsync(new("query"))).Status); Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task MapsEmbeddingCancellationAndPreservesPartialWarnings()
    {
        var cancelled = new FakeGenerator(EmbeddingStatus.Cancelled);
        Assert.Equal(HybridSearchStatus.Cancelled, (await Engine(new FakeSearch([]), new FakeSemantic([], SemanticSearchStatus.Success), [cancelled]).SearchAsync(new("query"))).Status);
        Assert.Equal(HybridSearchStatus.Cancelled, (await Engine(new FakeSearch([Hit(Guid.NewGuid(), 1)]), new FakeSemantic([], SemanticSearchStatus.Cancelled)).SearchAsync(new("query"))).Status);
        var partial = await Engine(new FakeSearch([Hit(Guid.NewGuid(), 1)]), new FakeSemantic([], SemanticSearchStatus.PartialSuccess, [new("SEMANTIC_LIMIT", "limit")])).SearchAsync(new("query"));
        Assert.Equal(HybridSearchStatus.PartialSuccess, partial.Status); Assert.Equal("SEMANTIC_LIMIT", Assert.Single(partial.Warnings!).Code);
    }

    [Fact]
    public async Task ReturnsFailedOnlyWhenBothSearchSourcesFailAndNoResultsForEmptySources()
    {
        var failed = await Engine(new FakeSearch([], SearchStatus.Failed), new FakeSemantic([], SemanticSearchStatus.Failed)).SearchAsync(new("query"));
        Assert.Equal(HybridSearchStatus.Failed, failed.Status);
        var unavailable = await Engine(new FakeSearch([], SearchStatus.Failed), new FakeSemantic([], SemanticSearchStatus.Success), []).SearchAsync(new("query"));
        Assert.Equal(HybridSearchStatus.Failed, unavailable.Status);
        Assert.Equal(HybridSearchStatus.NoResults, (await Engine([], []).SearchAsync(new("query"))).Status);
        var noContext = await Engine([], [], generators: []).SearchAsync(new("query"));
        Assert.Equal(HybridSearchStatus.NoResults, noContext.Status);
        Assert.Contains(noContext.Warnings!, warning => warning.Code == "HYBRID_SEMANTIC_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task IsSafeForConcurrentRequests()
    {
        var engine = Engine([Hit(Guid.NewGuid(), 1)], []);
        var responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => engine.SearchAsync(new("query"))));
        Assert.All(responses, response => Assert.Equal(HybridSearchStatus.Success, response.Status));
    }

    [Fact]
    public async Task IntegratesEmbeddingVectorIndexSemanticSearchAndHybridSearch()
    {
        var memoryItemId = Guid.NewGuid();
        using var index = new InMemoryVectorIndex(Options.Create(new VectorIndexOptions { MaxRecords = 10, MaxQueryResults = 10, MaxMetadataEntries = 2 }));
        var embedding = new EmbeddingVector(Guid.NewGuid(), [1, 0], 2, "m", "1", "hash", DateTimeOffset.UtcNow);
        var added = await index.AddAsync(new(new VectorRecord(Guid.NewGuid(), "fake", embedding, null, null, memoryItemId, null, "hash", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        Assert.Equal(VectorIndexStatus.Success, added.Status);

        var semantic = new SemanticSearchEngine(index, Options.Create(new SemanticSearchOptions { MaxCandidates = 100, DefaultTopK = 5, MaxTopK = 100, CandidatePageSize = 5 }));
        var engine = new HybridSearchEngine(new FakeSearch([Hit(memoryItemId, 10)]), semantic, [new FakeGenerator()], Options.Create(new HybridSearchOptions()));
        var response = await engine.SearchAsync(new("query"));

        var result = Assert.Single(response.Results!);
        Assert.Equal(HybridSearchStatus.Success, response.Status); Assert.Equal(memoryItemId, result.MemoryId); Assert.Equal(1f, result.SemanticScore); Assert.Equal(1f, result.HybridScore);
    }

    private static HybridSearchEngine Engine(IReadOnlyList<SearchHit> lexical, IReadOnlyList<SemanticSearchResult> semantic, IEnumerable<IEmbeddingGenerator>? generators=null, SemanticSearchStatus semanticStatus=SemanticSearchStatus.Success) => Engine(new FakeSearch(lexical), new FakeSemantic(semantic, semanticStatus), generators);
    private static HybridSearchEngine Engine(ISearchEngine lexical, ISemanticSearchEngine semantic, IEnumerable<IEmbeddingGenerator>? generators=null) => new(lexical, semantic, generators ?? [new FakeGenerator()], Options.Create(new HybridSearchOptions()));
    private static SearchHit Hit(Guid id,int score) => new(Guid.NewGuid(),Guid.NewGuid(),id,Guid.NewGuid(),KnowledgeItemType.Text,"",new(Guid.NewGuid(),"safe","display"),new("text"),new(score,0,0,0,0,0,0),0);
    private static SemanticSearchResult Semantic(Guid id,float score) => new(Guid.NewGuid(),score,"fake","m","1",2,new(Guid.NewGuid(),null,null,id,null,Guid.NewGuid()));
    private sealed class FakeSearch(IReadOnlyList<SearchHit> hits, SearchStatus status = SearchStatus.Success, IReadOnlyList<SearchWarning>? warnings = null):ISearchEngine { public SearchQuery? LastQuery { get; private set; } public Task<SearchResult> SearchAsync(SearchQuery query,CancellationToken cancellationToken=default) { LastQuery=query; return Task.FromResult(new SearchResult(status,hits,warnings)); } }
    private sealed class FakeSemantic(IReadOnlyList<SemanticSearchResult> results,SemanticSearchStatus status, IReadOnlyList<SemanticSearchWarning>? warnings = null):ISemanticSearchEngine { public SemanticSearchRequest? LastRequest { get; private set; } public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request,CancellationToken cancellationToken=default) { LastRequest=request; return Task.FromResult(new SemanticSearchResponse(status,results,warnings)); } }
    private sealed class ThrowingSearch : ISearchEngine { public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) => throw new InvalidOperationException(); }
    private sealed class CancellingSearch(CancellationTokenSource cancellation) : ISearchEngine { public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) { cancellation.Cancel(); return Task.FromResult(new SearchResult(SearchStatus.Success, [])); } }
    private sealed class ThrowingSemantic : ISemanticSearchEngine { public Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException(); }
    private sealed class FakeGenerator(EmbeddingStatus status = EmbeddingStatus.Success): IEmbeddingGenerator { public int Calls { get; private set; } public EmbeddingModelInfo ModelInfo=>new("fake","m","1",2,null,false); public Task<EmbeddingResult> GenerateAsync(EmbeddingInput input,CancellationToken cancellationToken=default) { Calls++; return Task.FromResult(status == EmbeddingStatus.Success ? new EmbeddingResult(status,new EmbeddingVector(input.Id,[1,0],2,"m","1","H",DateTimeOffset.UtcNow)) : new EmbeddingResult(status)); } public Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request,CancellationToken cancellationToken=default)=>throw new NotSupportedException(); }
    private sealed class ThrowingGenerator : IEmbeddingGenerator { public EmbeddingModelInfo ModelInfo => new("fake", "m", "1", 2, null, false); public Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default) => throw new InvalidOperationException(); public Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}
