namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionAuditOptions
{
    public const string SectionName = "ExecutionAudit";
    public int MaxEvents { get; init; } = 1_000;
}
