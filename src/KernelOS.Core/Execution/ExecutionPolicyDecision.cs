namespace KernelOS.Core.Execution;

public sealed record ExecutionPolicyDecision(ExecutionPolicyDecisionType Type, ExecutionRiskLevel RiskLevel, ExecutionPolicyReason Reason);
