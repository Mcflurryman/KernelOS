using KernelOS.Core.Execution;

namespace KernelOS.Core.Audit;

public sealed record AuditEvent(
    AuditFlowId FlowId,
    DateTimeOffset TimestampUtc,
    AuditEventType EventType,
    Guid? PlanId = null,
    Guid? TaskId = null,
    Guid? PendingExecutionId = null,
    Guid? ApprovalId = null,
    ExecutionOrigin? Origin = null,
    string? Status = null,
    ExecutionPolicyDecisionType? PolicyDecision = null,
    ExecutionRiskLevel? Risk = null,
    string? ReasonCode = null,
    string? ErrorCode = null,
    TimeSpan? Duration = null);
