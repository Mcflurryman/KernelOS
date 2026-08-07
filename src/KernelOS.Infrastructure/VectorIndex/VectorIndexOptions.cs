namespace KernelOS.Infrastructure.VectorIndex;

public sealed class VectorIndexOptions
{
    public const string SectionName = "VectorIndex";
    public int MaxRecords { get; init; } = 100000;
    public int MaxQueryResults { get; init; } = 1000;
    public int MaxMetadataEntries { get; init; } = 50;
}
