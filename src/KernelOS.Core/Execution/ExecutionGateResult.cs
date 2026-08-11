namespace KernelOS.Core.Execution;

public sealed record ExecutionGateResult(ExecutionGateStatus Status, ExecutionPolicyDecision Decision, Guid? ApprovalId = null);
