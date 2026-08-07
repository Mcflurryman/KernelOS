namespace KernelOS.Infrastructure.Search;

public sealed class SearchOptions
{
    public const string SectionName = "Search";
    public int MaxQueryLength { get; init; } = 2000;
    public int MaxTokens { get; init; } = 100;
    public int MaxCandidateDocuments { get; init; } = 5000;
    public int MaxResults { get; init; } = 100;
}
