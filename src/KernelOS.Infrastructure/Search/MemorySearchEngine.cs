using System.Text;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Core.Search;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Search;

public sealed class MemorySearchEngine : ISearchEngine
{
    private const int ExactWeight = 100;
    private const int PrefixWeight = 40;
    private const int TokenWeight = 10;
    private const int HeadingWeight = 5;
    private const int MetadataWeight = 5;
    private readonly IMemoryStore memoryStore;
    private readonly SearchOptionsSnapshot options;

    public MemorySearchEngine(IMemoryStore memoryStore, IOptions<SearchOptions> options)
    {
        this.memoryStore = memoryStore;
        var value = options.Value;
        this.options = new(value.MaxQueryLength, value.MaxTokens, value.MaxCandidateDocuments, value.MaxResults);
    }

    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(SearchStatus.Cancelled);
        if (!Valid(query)) return new(SearchStatus.InvalidQuery, Error: "The search query is invalid.");
        if (query.Text?.Length > options.MaxQueryLength) return new(SearchStatus.TooLarge, Error: "The search query exceeds the configured limit.");

        try
        {
            var normalizedText = Normalize(query.Text ?? string.Empty);
            var tokens = Tokenize(normalizedText).ToArray();
            if (tokens.Length > options.MaxTokens) return new(SearchStatus.TooLarge, Error: "The search query has too many tokens.");
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = await memoryStore.QueryAsync(new(Limit: options.MaxCandidateDocuments), cancellationToken);
            if (candidates.Status == MemoryStatus.Cancelled) return new(SearchStatus.Cancelled);
            if (candidates.Status != MemoryStatus.Success) return new(SearchStatus.Failed, Error: "Memory candidates could not be queried.");

            var hits = new List<(SearchHit Hit, DateTimeOffset UpdatedAt)>();
            foreach (var document in candidates.Documents ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!DocumentMatches(document, query)) continue;
                foreach (var (item, order) in document.Items.Select((item, order) => (item, order)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ItemMatches(item, order, query, normalizedText, tokens, query.MetadataKey is not null, out var score)) continue;
                    hits.Add((new(document.Id, document.KnowledgeDocumentId, item.Id, item.KnowledgeItemId, item.Type, item.Content, Copy(item.Source), Copy(item.Metadata), score, order), document.UpdatedAt));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = hits.OrderByDescending(hit => hit.Hit.Score.Total).ThenByDescending(hit => hit.UpdatedAt).ThenBy(hit => hit.Hit.MemoryDocumentId).ThenBy(hit => hit.Hit.MemoryItemId).Skip(query.Offset).Take(Math.Min(query.Limit, options.MaxResults)).Select(hit => hit.Hit).ToArray();
            return result.Length == 0 ? new(SearchStatus.NoResults, result) : new(SearchStatus.Success, result);
        }
        catch (OperationCanceledException) { return new(SearchStatus.Cancelled); }
        catch { return new(SearchStatus.Failed, Error: "Search failed."); }
    }

    private static bool Valid(SearchQuery query) => query.Limit > 0 && query.Offset >= 0 && !(query.Exact && query.Prefix) && (!string.IsNullOrWhiteSpace(query.Text) || query.ItemTypes?.Count > 0 || query.KnowledgeDocumentId.HasValue || query.MemoryDocumentId.HasValue || query.MetadataKey is not null);
    private static bool DocumentMatches(MemoryDocument document, SearchQuery query) => (!query.KnowledgeDocumentId.HasValue || document.KnowledgeDocumentId == query.KnowledgeDocumentId) && (!query.MemoryDocumentId.HasValue || document.Id == query.MemoryDocumentId) && (query.MetadataKey is null || document.Metadata.Properties?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);
    private static bool ItemMatches(MemoryItem item, int order, SearchQuery query, string normalizedText, string[] queryTokens, bool metadataMatched, out SearchScore score)
    {
        score = new(0, 0, 0, 0, 0, 0, 0);
        if (query.ItemTypes?.Count > 0 && !query.ItemTypes.Contains(item.Type)) return false;
        var normalizedContent = Normalize(item.Content);
        var itemTokens = Tokenize(normalizedContent).ToArray();
        var exact = !string.IsNullOrEmpty(normalizedText) && normalizedContent == normalizedText;
        var prefix = query.Prefix && queryTokens.Length > 0 && queryTokens.All(queryToken => itemTokens.Any(itemToken => itemToken.StartsWith(queryToken, StringComparison.Ordinal)));
        var tokenCount = queryTokens.Count(queryToken => itemTokens.Contains(queryToken, StringComparer.Ordinal));
        var tokenMatch = queryTokens.Length == 0 || queryTokens.All(queryToken => itemTokens.Contains(queryToken, StringComparer.Ordinal));
        if (query.Exact ? !exact : query.Prefix ? !prefix : !tokenMatch) return false;
        var exactScore = exact && query.Exact ? ExactWeight : 0;
        var prefixScore = prefix ? PrefixWeight : 0;
        var tokenScore = tokenCount * TokenWeight;
        var typeScore = item.Type == KnowledgeItemType.Heading ? HeadingWeight : 0;
        var positionScore = Math.Max(0, 2 - order);
        var metadataScore = metadataMatched ? MetadataWeight : 0;
        var total = exactScore + prefixScore + tokenScore + positionScore + typeScore + metadataScore;
        score = new(total, exactScore, tokenScore, prefixScore, positionScore, typeScore, metadataScore);
        return true;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        var whitespace = false;
        foreach (var character in value.Normalize(NormalizationForm.FormC).Trim())
        {
            if (char.IsWhiteSpace(character)) { whitespace = builder.Length > 0; continue; }
            if (whitespace) { builder.Append(' '); whitespace = false; }
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character)) { builder.Append(character); continue; }
            if (builder.Length > 0) { yield return builder.ToString(); builder.Clear(); }
        }
        if (builder.Length > 0) yield return builder.ToString();
    }

    private static KnowledgeSource Copy(KnowledgeSource source) => source with { Locator = source.Locator is null ? null : source.Locator with { } };
    private static KnowledgeMetadata Copy(KnowledgeMetadata metadata) => metadata with { Properties = metadata.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) };
}
