using KernelOS.Core.HybridSearch;
using KernelOS.Core.Knowledge;

namespace KernelOS.Core.Context;

public enum ContextStatus { Success, PartialSuccess, NoContext, InvalidRequest, Cancelled, Failed }
public sealed record ContextWarning(string Code, string Message);
public sealed record ContextBudget(int MaxTokens, int MaxItems, float CharactersPerTokenEstimate);
public sealed record ContextOptionsSnapshot(int DefaultMaxTokens, int MaxAllowedTokens, int DefaultMaxItems, int MaxAllowedItems, float CharactersPerTokenEstimate);
public sealed record ContextBuildRequest(IReadOnlyList<HybridSearchResult>? Results, int? MaxTokens = null, int? MaxItems = null, float MinimumHybridScore = 0);
public sealed record ContextItem(Guid MemoryDocumentId, Guid MemoryItemId, string Content, float HybridScore, KnowledgeSource Source, int Order, int EstimatedTokens, string CitationId);
public sealed record ContextCitation(string CitationId, Guid MemoryDocumentId, Guid MemoryItemId, KnowledgeSource Source);
public sealed record ContextPack(IReadOnlyList<ContextItem> Items, IReadOnlyList<ContextCitation> Citations, int EstimatedTokens, int MaxTokens, bool Truncated, IReadOnlyList<ContextWarning>? Warnings = null);
public sealed record ContextBuildResult(ContextStatus Status, ContextPack? Pack = null, IReadOnlyList<ContextWarning>? Warnings = null, string? Error = null);
