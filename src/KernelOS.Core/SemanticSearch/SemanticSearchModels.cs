using KernelOS.Core.Embeddings;
using KernelOS.Core.VectorIndex;

namespace KernelOS.Core.SemanticSearch;

public enum SemanticSearchStatus { Success, PartialSuccess, InvalidRequest, Cancelled, Failed }
public sealed record SemanticSearchWarning(string Code, string Message);
public sealed record SemanticSearchRequest(EmbeddingVector? QueryEmbedding, string? Provider, int? TopK = null, float MinimumScore = 0, string? Model = null, string? Version = null, int? Dimensions = null);
public sealed record SemanticSearchResult(Guid VectorRecordId, float Score, string Provider, string Model, string? Version, int Dimensions, VectorReference Reference);
public sealed record SemanticSearchResponse(SemanticSearchStatus Status, IReadOnlyList<SemanticSearchResult>? Results = null, IReadOnlyList<SemanticSearchWarning>? Warnings = null, string? Error = null);
