namespace KernelOS.Core.Execution;

public sealed record ExecutionConfirmationRequest(
    Guid PendingExecutionId,
    Guid PlanId,
    Guid TaskId,
    string ToolName,
    string Description,
    ExecutionRiskLevel RiskLevel,
    ExecutionPolicyReason Reason,
    string SafeArgumentSummary,
    DateTimeOffset ExpiresAt,
    int TaskCount = 1);
