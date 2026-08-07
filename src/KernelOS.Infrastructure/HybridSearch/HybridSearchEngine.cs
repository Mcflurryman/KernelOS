using KernelOS.Core.Embeddings;
using KernelOS.Core.HybridSearch;
using KernelOS.Core.Search;
using KernelOS.Core.SemanticSearch;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.HybridSearch;

public sealed class HybridSearchEngine : IHybridSearchEngine
{
    private readonly ISearchEngine lexical;
    private readonly ISemanticSearchEngine semantic;
    private readonly IEmbeddingGenerator? embeddings;
    private readonly int generatorCount;
    private readonly HybridSearchOptions options;

    public HybridSearchEngine(ISearchEngine lexical, ISemanticSearchEngine semantic, IEnumerable<IEmbeddingGenerator> generators, IOptions<HybridSearchOptions> options)
    {
        this.lexical = lexical;
        this.semantic = semantic;
        this.options = options.Value;
        var configuredGenerators = generators.ToArray();
        generatorCount = configuredGenerators.Length;
        embeddings = generatorCount == 1 ? configuredGenerators[0] : null;
    }

    public async Task<HybridSearchResponse> SearchAsync(HybridSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(HybridSearchStatus.Cancelled);

        var topK = request.TopK ?? options.DefaultTopK;
        if (string.IsNullOrWhiteSpace(request.Query) || topK <= 0 || topK > options.MaxTopK || request.MinimumSemanticScore is < 0 or > 1)
            return new(HybridSearchStatus.InvalidRequest);

        if (embeddings is null)
            return new(generatorCount == 0 ? HybridSearchStatus.ProviderUnavailable : HybridSearchStatus.Failed,
                Error: generatorCount == 0 ? null : "Hybrid search requires an explicit embedding provider selection.");

        var candidateLimit = (int)Math.Min(options.MaxTopK, (long)topK * options.CandidateMultiplier);
        var lexicalResult = await lexical.SearchAsync(new SearchQuery(request.Query, Limit: candidateLimit), cancellationToken);
        if (lexicalResult.Status == SearchStatus.Cancelled || cancellationToken.IsCancellationRequested)
            return new(HybridSearchStatus.Cancelled);

        var embedding = await embeddings.GenerateAsync(new EmbeddingInput(Guid.NewGuid(), request.Query), cancellationToken);
        if (embedding.Status == EmbeddingStatus.Cancelled || cancellationToken.IsCancellationRequested)
            return new(HybridSearchStatus.Cancelled);
        if (embedding.Status != EmbeddingStatus.Success || embedding.Vector is null)
            return new(HybridSearchStatus.ProviderUnavailable);

        var semanticResult = await semantic.SearchAsync(
            new(embedding.Vector, embeddings.ModelInfo.Provider, candidateLimit, request.MinimumSemanticScore), cancellationToken);
        if (semanticResult.Status == SemanticSearchStatus.Cancelled || cancellationToken.IsCancellationRequested)
            return new(HybridSearchStatus.Cancelled);

        var lexicalFailed = lexicalResult.Status == SearchStatus.Failed;
        var semanticFailed = semanticResult.Status == SemanticSearchStatus.Failed;
        if (lexicalFailed && semanticFailed)
            return new(HybridSearchStatus.Failed, Error: "Hybrid search sources are unavailable.");

        var warnings = new List<HybridSearchWarning>();
        if (lexicalFailed) warnings.Add(new("HYBRID_LEXICAL_UNAVAILABLE", "Lexical search is unavailable."));
        if (semanticFailed) warnings.Add(new("HYBRID_SEMANTIC_UNAVAILABLE", "Semantic search is unavailable."));
        warnings.AddRange((lexicalResult.Warnings ?? []).Select(warning => new HybridSearchWarning(warning.Code, warning.Message)));
        warnings.AddRange((semanticResult.Warnings ?? []).Select(warning => new HybridSearchWarning(warning.Code, warning.Message)));

        var maxLexical = (lexicalResult.Hits ?? []).Select(hit => hit.Score.Total).DefaultIfEmpty(0).Max();
        var entries = new Dictionary<Guid, HybridSearchResult>();
        foreach (var hit in lexicalResult.Hits ?? [])
        {
            var score = maxLexical > 0 ? hit.Score.Total / (float)maxLexical : 0;
            entries[hit.MemoryItemId] = new(hit.MemoryItemId, null, score, 0, 0, null, null, null);
        }

        foreach (var hit in semanticResult.Results ?? [])
        {
            var id = hit.Reference.MemoryItemId ?? hit.VectorRecordId;
            entries.TryGetValue(id, out var prior);
            entries[id] = new(id, hit.VectorRecordId, prior?.LexicalScore ?? 0, hit.Score, 0, hit.Provider, hit.Model, hit.Version);
        }

        var totalWeight = options.LexicalWeight + options.SemanticWeight;
        var results = entries.Values
            .Select(item => item with { HybridScore = (options.LexicalWeight / totalWeight * item.LexicalScore) + (options.SemanticWeight / totalWeight * item.SemanticScore) })
            .OrderByDescending(item => item.HybridScore)
            .ThenByDescending(item => item.SemanticScore)
            .ThenByDescending(item => item.LexicalScore)
            .ThenBy(item => item.MemoryId)
            .Take(topK)
            .ToArray();

        var isPartial = lexicalResult.Status == SearchStatus.Failed || semanticResult.Status is SemanticSearchStatus.Failed or SemanticSearchStatus.PartialSuccess || warnings.Count > 0;
        var status = results.Length == 0 ? HybridSearchStatus.NoResults : isPartial ? HybridSearchStatus.PartialSuccess : HybridSearchStatus.Success;
        return new(status, results, warnings.Count == 0 ? null : warnings);
    }
}
