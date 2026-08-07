namespace KernelOS.Infrastructure.SemanticSearch;

public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";
    public int MaxCandidates { get; init; } = 10000;
    public int DefaultTopK { get; init; } = 10;
    public int MaxTopK { get; init; } = 100;
    public int CandidatePageSize { get; init; } = 500;
}
