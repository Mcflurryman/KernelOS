namespace KernelOS.Infrastructure.SemanticIndex;

public sealed class SemanticIndexMaintenanceOptions
{
    public const string SectionName = "SemanticIndexMaintenance";
    public int QueueCapacity { get; init; } = 256;
}
