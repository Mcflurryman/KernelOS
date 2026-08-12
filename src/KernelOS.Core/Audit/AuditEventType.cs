namespace KernelOS.Core.Audit;

public enum AuditEventType
{
    KaiRequestStarted,
    KaiRouteSelected,
    PlanCreated,
    PreflightStarted,
    TaskAuthorizationEvaluated,
    PreflightCompleted,
    PendingExecutionCreated,
    ExecutionApproved,
    ExecutionRejected,
    PlanExecutionStarted,
    TaskExecutionStarted,
    TaskExecutionCompleted,
    TaskExecutionFailed,
    TaskExecutionCancelled,
    PlanExecutionCompleted,
    PlanExecutionFailed,
    PlanExecutionCancelled,
    DirectReadOnlyExecutionStarted,
    DirectReadOnlyExecutionCompleted,
    DirectReadOnlyExecutionFailed
}
