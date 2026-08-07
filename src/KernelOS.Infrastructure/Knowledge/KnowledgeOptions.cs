namespace KernelOS.Infrastructure.Knowledge;

public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";
    public int MaxItemCharacters { get; init; } = 2000;
    public int ChunkOverlapCharacters { get; init; } = 200;
    public int MaxItemsPerDocument { get; init; } = 5000;
    public bool IncludeMetadataItems { get; init; } = true;
}
