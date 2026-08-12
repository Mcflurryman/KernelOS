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
    private readonly HybridSearchOptions options;

    public HybridSearchEngine(ISearchEngine lexical, ISemanticSearchEngine semantic, IEnumerable<IEmbeddingGenerator> generators, IOptions<HybridSearchOptions> options)
    {
        this.lexical = lexical;
        this.semantic = semantic;
        this.options = options.Value;
        var configuredGenerators = generators.ToArray();
        embeddings = configuredGenerators.Length == 1 ? configuredGenerators[0] : null;
    }

    public async Task<HybridSearchResponse> SearchAsync(HybridSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(HybridSearchStatus.Cancelled);

        var topK = request.TopK ?? options.DefaultTopK;
        if (string.IsNullOrWhiteSpace(request.Query) || topK <= 0 || topK > options.MaxTopK || request.MinimumSemanticScore is < 0 or > 1)
            return new(HybridSearchStatus.InvalidRequest);

        var candidateLimit = (int)Math.Min(options.MaxTopK, (long)topK * options.CandidateMultiplier);
        SearchResult lexicalResult;
        try
        {
            lexicalResult = await lexical.SearchAsync(new SearchQuery(request.Query, Limit: candidateLimit), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new(HybridSearchStatus.Cancelled);
        }
        catch
        {
            lexicalResult = new(SearchStatus.Failed, Error: "Lexical search failed.");
        }
        if (lexicalResult.Status == SearchStatus.Cancelled || cancellationToken.IsCancellationRequested)
            return new(HybridSearchStatus.Cancelled);

        var lexicalFailed = lexicalResult.Status == SearchStatus.Failed;
        var warnings = new List<HybridSearchWarning>();
        if (lexicalFailed) warnings.Add(new("HYBRID_LEXICAL_FAILED", "Lexical search failed."));
        warnings.AddRange((lexicalResult.Warnings ?? []).Select(warning => new HybridSearchWarning(warning.Code, warning.Message)));

        SemanticSearchResponse? semanticResult = null;
        var semanticUnavailable = embeddings is null;
        var semanticFailed = false;

        if (embeddings is null)
        {
            warnings.Add(new("HYBRID_SEMANTIC_PROVIDER_UNAVAILABLE", "Semantic search could not select an embedding provider."));
        }
        else
        {
            EmbeddingResult? embedding;
            try
            {
                embedding = await embeddings.GenerateAsync(new EmbeddingInput(Guid.NewGuid(), request.Query), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new(HybridSearchStatus.Cancelled);
            }
            catch
            {
                semanticFailed = true;
                warnings.Add(new("HYBRID_SEMANTIC_EMBEDDING_FAILED", "Semantic query embedding failed."));
                embedding = null;
            }

            if (embedding?.Status == EmbeddingStatus.Cancelled || cancellationToken.IsCancellationRequested)
                return new(HybridSearchStatus.Cancelled);

            if (!semanticFailed && (embedding?.Status != EmbeddingStatus.Success || embedding.Vector is null))
            {
                semanticFailed = true;
                warnings.Add(new("HYBRID_SEMANTIC_EMBEDDING_FAILED", "Semantic query embedding failed."));
            }

            if (!semanticFailed)
            {
                var queryEmbedding = embedding?.Vector;
                try
                {
                    semanticResult = await semantic.SearchAsync(
                        new(queryEmbedding, embeddings.ModelInfo.Provider, candidateLimit, request.MinimumSemanticScore), cancellationToken);
                    if (semanticResult.Status == SemanticSearchStatus.Cancelled || cancellationToken.IsCancellationRequested)
                        return new(HybridSearchStatus.Cancelled);

                    semanticFailed = semanticResult.Status == SemanticSearchStatus.Failed;
                    if (semanticFailed) warnings.Add(new("HYBRID_SEMANTIC_SEARCH_FAILED", "Semantic search failed."));
                    warnings.AddRange((semanticResult.Warnings ?? []).Select(warning => new HybridSearchWarning(warning.Code, warning.Message)));
                }
                catch (OperationCanceledException)
                {
                    return new(HybridSearchStatus.Cancelled);
                }
                catch
                {
                    semanticFailed = true;
                    warnings.Add(new("HYBRID_SEMANTIC_SEARCH_FAILED", "Semantic search failed."));
                }
            }
        }

        if (lexicalFailed && (semanticUnavailable || semanticFailed))
            return new(HybridSearchStatus.Failed, Warnings: warnings, Error: "Hybrid search sources failed.");

        var maxLexical = (lexicalResult.Hits ?? []).Select(hit => hit.Score.Total).DefaultIfEmpty(0).Max();
        var entries = new Dictionary<Guid, HybridSearchResult>();
        foreach (var hit in lexicalResult.Hits ?? [])
        {
            var score = maxLexical > 0 ? hit.Score.Total / (float)maxLexical : 0;
            entries[hit.MemoryItemId] = new(hit.MemoryItemId, null, score, 0, 0, null, null, null);
        }

        foreach (var hit in semanticResult?.Results ?? [])
        {
            var id = hit.Reference.MemoryItemId ?? hit.VectorRecordId;
            entries.TryGetValue(id, out var prior);
            entries[id] = new(id, hit.VectorRecordId, prior?.LexicalScore ?? 0, hit.Score, 0, hit.Provider, hit.Model, hit.Version);
        }

        var lexicalWeight = semanticUnavailable || semanticFailed ? 1f : options.LexicalWeight / (options.LexicalWeight + options.SemanticWeight);
        var semanticWeight = lexicalFailed ? 1f : options.SemanticWeight / (options.LexicalWeight + options.SemanticWeight);
        var results = entries.Values
            .Select(item => item with { HybridScore = (lexicalWeight * item.LexicalScore) + (semanticWeight * item.SemanticScore) })
            .OrderByDescending(item => item.HybridScore)
            .ThenByDescending(item => item.SemanticScore)
            .ThenByDescending(item => item.LexicalScore)
            .ThenBy(item => item.MemoryId)
            .Take(topK)
            .ToArray();

        var isPartial = lexicalFailed || semanticUnavailable || semanticFailed || semanticResult?.Status == SemanticSearchStatus.PartialSuccess || warnings.Count > 0;
        var status = results.Length == 0 ? HybridSearchStatus.NoResults : isPartial ? HybridSearchStatus.PartialSuccess : HybridSearchStatus.Success;
        return new(status, results, warnings.Count == 0 ? null : warnings);
    }
}
