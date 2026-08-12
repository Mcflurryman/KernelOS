using KernelOS.Core.Execution;
using KernelOS.Core.Audit;
using KernelOS.Core.Planning;

namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionPreflight(IExecutionGate gate, IExecutionAuditWriter? audit = null, TimeProvider? timeProvider = null) : IExecutionPreflight
{
    public async Task<ExecutionPreflightResult> EvaluateAsync(Plan plan, IReadOnlyDictionary<Guid, Guid>? approvalIds = null, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Guid, ExecutionGateResult>();
        var status = ExecutionGateStatus.Authorized;
        var context = plan.AuditContext;
        if (context is not null)
            _ = audit?.WriteAsync(new AuditEvent(context.FlowId, (timeProvider ?? TimeProvider.System).GetUtcNow(), AuditEventType.PreflightStarted, plan.Id, Origin: context.Origin), CancellationToken.None);
        foreach (var task in plan.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var approvalId = approvalIds is not null && approvalIds.TryGetValue(task.Id, out var value) ? value : (Guid?)null;
            var result = await gate.EvaluateAsync(plan.Id, task, approvalId, consumeApproval: false, cancellationToken);
            results[task.Id] = result;
            if (context is not null)
                _ = audit?.WriteAsync(new AuditEvent(context.FlowId, (timeProvider ?? TimeProvider.System).GetUtcNow(), AuditEventType.TaskAuthorizationEvaluated, plan.Id, task.Id, Origin: context.Origin, Status: result.Status.ToString(), PolicyDecision: result.Decision.Type, Risk: result.Decision.RiskLevel, ReasonCode: result.Decision.Reason.ToString()), CancellationToken.None);
            if (result.Status == ExecutionGateStatus.Denied) status = ExecutionGateStatus.Denied;
            else if (result.Status == ExecutionGateStatus.RequiresConfirmation && status != ExecutionGateStatus.Denied) status = ExecutionGateStatus.RequiresConfirmation;
        }

        if (context is not null)
            _ = audit?.WriteAsync(new AuditEvent(context.FlowId, (timeProvider ?? TimeProvider.System).GetUtcNow(), AuditEventType.PreflightCompleted, plan.Id, Origin: context.Origin, Status: status.ToString()), CancellationToken.None);
        return new(status, results);
    }
}
