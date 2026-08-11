namespace KernelOS.Core.Execution;

public sealed record ToolExecutionMetadata(bool IsReadOnly, bool HasSideEffects, bool IsExplicitlyDenied, ExecutionRiskLevel RiskLevel)
{
    public static ToolExecutionMetadata Unknown { get; } = new(false, false, false, ExecutionRiskLevel.Medium);
}
