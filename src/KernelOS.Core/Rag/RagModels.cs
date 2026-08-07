using KernelOS.Core.Context;

namespace KernelOS.Core.Rag;

public enum RagStatus { Success, PartialSuccess, NoContext, InvalidRequest, ProviderUnavailable, Cancelled, Failed }
public sealed record RagWarning(string Code, string Message);
public sealed record RagRequest(string? Query, int? TopK = null, int? MaxContextTokens = null, int? MaxContextItems = null, float MinimumHybridScore = 0);
public sealed record RagCitation(string CitationId, Guid MemoryDocumentId, Guid MemoryItemId, string SafeReference, string DisplayReference);
public sealed record RagContextInfo(int RetrievedCandidates, int ContextItems, int EstimatedTokens, bool Truncated);
public sealed record RagOptionsSnapshot(int MaxQueryCharacters, int DefaultTopK, int MaxTopK, int DefaultContextTokens, int MaxContextTokens, bool RequireCitations);
public sealed record RagResponse(RagStatus Status, string Answer = "", IReadOnlyList<RagCitation>? Citations = null, IReadOnlyList<RagWarning>? Warnings = null, RagContextInfo? Context = null, string? Model = null, string? Error = null);
