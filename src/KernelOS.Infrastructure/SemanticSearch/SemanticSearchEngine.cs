using KernelOS.Core.Embeddings;
using KernelOS.Core.SemanticSearch;
using KernelOS.Core.VectorIndex;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.SemanticSearch;

public sealed class SemanticSearchEngine(IVectorIndex vectorIndex, IOptions<SemanticSearchOptions> options) : ISemanticSearchEngine
{
    private readonly SemanticSearchOptions options = options.Value;

    public async Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default)
    {
        var topK = request.TopK ?? options.DefaultTopK;
        if (cancellationToken.IsCancellationRequested) return new(SemanticSearchStatus.Cancelled);
        if (request.QueryEmbedding is null || !request.QueryEmbedding.IsValid() || !HasMagnitude(request.QueryEmbedding.Values) || string.IsNullOrWhiteSpace(request.Provider) || topK <= 0 || topK > options.MaxTopK || request.MinimumScore is < 0 or > 1) return new(SemanticSearchStatus.InvalidRequest, Error: "The semantic search request is invalid.");
        try
        {
            var results = new List<SemanticSearchResult>(); var offset = 0; var examined = 0; var limited = false;
            while (examined < options.MaxCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var size = Math.Min(options.CandidatePageSize, options.MaxCandidates - examined);
                var indexed = await vectorIndex.QueryAsync(new(Provider: request.Provider, Model: request.Model ?? request.QueryEmbedding.Model, Version: request.Version ?? request.QueryEmbedding.ModelVersion, Dimensions: request.Dimensions ?? request.QueryEmbedding.Dimensions, Limit: size, Offset: offset), cancellationToken);
                if (indexed.Status == VectorIndexStatus.Cancelled) return new(SemanticSearchStatus.Cancelled);
                if (indexed.Status != VectorIndexStatus.Success) return new(SemanticSearchStatus.Failed, Error: "Vector index query failed.");
                var page = indexed.Records ?? []; examined += page.Count; offset += page.Count;
                foreach (var record in page)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Compatible(request.QueryEmbedding, request.Provider, record) || !HasMagnitude(record.Embedding.Values)) continue;
                    var score = Score(request.QueryEmbedding.Values, record.Embedding.Values);
                    if (score >= request.MinimumScore) results.Add(new(record.Id, score, record.Provider, record.Embedding.Model, record.Embedding.ModelVersion, record.Embedding.Dimensions, new(record.Id, record.MemoryDocumentId, record.KnowledgeDocumentId, record.MemoryItemId, record.KnowledgeItemId, record.Embedding.InputId)));
                }
                if (page.Count < size) break;
                if (examined >= options.MaxCandidates) limited = true;
            }
            var warnings = limited ? new[] { new SemanticSearchWarning("SEMANTIC_CANDIDATE_LIMIT", "Semantic search reached the configured candidate limit.") } : null;
            return new(limited ? SemanticSearchStatus.PartialSuccess : SemanticSearchStatus.Success, results.OrderByDescending(result => result.Score).ThenBy(result => result.VectorRecordId).Take(topK).ToArray(), warnings);
        }
        catch (OperationCanceledException) { return new(SemanticSearchStatus.Cancelled); }
        catch { return new(SemanticSearchStatus.Failed, Error: "Semantic search failed."); }
    }
    private static bool Compatible(EmbeddingVector query, string provider, VectorRecord record) => string.Equals(provider, record.Provider, StringComparison.Ordinal) && string.Equals(query.Model, record.Embedding.Model, StringComparison.Ordinal) && string.Equals(query.ModelVersion, record.Embedding.ModelVersion, StringComparison.Ordinal) && query.Dimensions == record.Embedding.Dimensions;
    private static bool HasMagnitude(IReadOnlyList<float> values) => values.Any(value => value != 0f);
    private static float Score(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var index = 0; index < left.Count; index++) { dot += left[index] * right[index]; leftNorm += left[index] * left[index]; rightNorm += right[index] * right[index]; }
        var cosine = Math.Clamp(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)), -1d, 1d);
        return (float)Math.Clamp((cosine + 1d) / 2d, 0d, 1d);
    }
}
