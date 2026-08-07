using KernelOS.Core.Knowledge;

namespace KernelOS.Core.Search;

public enum SearchStatus { Success, NoResults, InvalidQuery, TooLarge, Cancelled, Failed }

public sealed record SearchFilter(IReadOnlyCollection<KnowledgeItemType>? ItemTypes = null, Guid? KnowledgeDocumentId = null, Guid? MemoryDocumentId = null, string? MetadataKey = null, string? MetadataValue = null);
public sealed record SearchQuery(string? Text = null, bool Exact = false, bool Prefix = false, IReadOnlyCollection<KnowledgeItemType>? ItemTypes = null, Guid? KnowledgeDocumentId = null, Guid? MemoryDocumentId = null, string? MetadataKey = null, string? MetadataValue = null, int Limit = 20, int Offset = 0);
public sealed record SearchScore(int Total, int ExactMatch, int TokenMatches, int PrefixMatch, int PositionBoost, int TypeBoost, int MetadataBoost);
public sealed record SearchHit(Guid MemoryDocumentId, Guid KnowledgeDocumentId, Guid MemoryItemId, Guid KnowledgeItemId, KnowledgeItemType ItemType, string Content, KnowledgeSource Source, KnowledgeMetadata Metadata, SearchScore Score, int Order);
public sealed record SearchWarning(string Code, string Message);
public sealed record SearchOptionsSnapshot(int MaxQueryLength, int MaxTokens, int MaxCandidateDocuments, int MaxResults);
public sealed record SearchResult(SearchStatus Status, IReadOnlyList<SearchHit>? Hits = null, IReadOnlyList<SearchWarning>? Warnings = null, string? Error = null);
