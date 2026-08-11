namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionPolicyOptions
{
    public const string SectionName = "ExecutionPolicy";
    public int ApprovalTtlMinutes { get; init; } = 5;
}
