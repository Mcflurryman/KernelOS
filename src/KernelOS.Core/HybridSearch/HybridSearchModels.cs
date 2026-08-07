namespace KernelOS.Core.HybridSearch;

public enum HybridSearchStatus { Success, PartialSuccess, NoResults, ProviderUnavailable, InvalidRequest, Cancelled, Failed }
public sealed record HybridSearchWarning(string Code, string Message);
public sealed record HybridSearchRequest(string? Query, int? TopK = null, float MinimumSemanticScore = 0);
public sealed record HybridSearchResult(Guid MemoryId, Guid? VectorId, float LexicalScore, float SemanticScore, float HybridScore, string? Provider, string? Model, string? Version);
public sealed record HybridSearchResponse(HybridSearchStatus Status, IReadOnlyList<HybridSearchResult>? Results = null, IReadOnlyList<HybridSearchWarning>? Warnings = null, string? Error = null);
