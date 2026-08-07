namespace KernelOS.Infrastructure.Memory;

public sealed class MemoryOptions
{
    public const string SectionName = "Memory";
    public int MaxDocuments { get; init; } = 10_000;
    public int MaxItemsPerDocument { get; init; } = 5_000;
    public int MaxQueryResults { get; init; } = 1_000;
}
