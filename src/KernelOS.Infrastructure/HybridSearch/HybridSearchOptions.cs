namespace KernelOS.Infrastructure.HybridSearch;
public sealed class HybridSearchOptions { public const string SectionName = "HybridSearch"; public float LexicalWeight { get; init; } = .4f; public float SemanticWeight { get; init; } = .6f; public int DefaultTopK { get; init; } = 10; public int MaxTopK { get; init; } = 100; public int CandidateMultiplier { get; init; } = 3; }
