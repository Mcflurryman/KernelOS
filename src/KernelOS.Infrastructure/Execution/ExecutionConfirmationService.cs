using KernelOS.Core.Execution;
using KernelOS.Core.Audit;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionConfirmationService(
    IExecutionPolicy policy,
    IExecutionApprovalStore approvals,
    IExecutionPendingStore pendingStore,
    IToolRegistry tools,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<ExecutionPolicyOptions> options,
    IExecutionAuditWriter? audit = null) : IExecutionConfirmationService
{
    private readonly TimeSpan ttl = TimeSpan.FromMinutes(options.Value.ApprovalTtlMinutes);

    public async Task<ExecutionConfirmationResult> CreatePendingAsync(Plan plan, Guid taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (plan is null
            || plan.Id == Guid.Empty
            || plan.Tasks.Count == 0
            || plan.Tasks.Any(task => task.Status != PlannerStatus.Planned))
        {
            return new(PendingExecutionStatus.NotConfirmable);
        }

        var evaluations = plan.Tasks.Select(task =>
        {
            var tool = tools.GetByName(task.ToolName);
            var decision = policy.Evaluate(new ExecutionPolicyRequest(
                plan.Id,
                task.Id,
                task.ToolName,
                tool?.ExecutionMetadata));
            return new { Task = task, Tool = tool, Decision = decision };
        }).ToArray();

        if (evaluations.Any(item =>
                item.Decision.Type == ExecutionPolicyDecisionType.Deny
                || item.Decision.Reason == ExecutionPolicyReason.UnknownToolRequiresConfirmation))
        {
            return new(PendingExecutionStatus.NotConfirmable);
        }
        var confirmable = evaluations.Where(item => item.Decision.Type == ExecutionPolicyDecisionType.RequireConfirmation).ToArray();
        if (confirmable.Length == 0) return new(PendingExecutionStatus.NotConfirmable);
        var representative = confirmable[0];

        var id = Guid.NewGuid();
        var expiresAt = timeProvider.GetUtcNow().Add(ttl);
        var risk = evaluations.Max(item => item.Decision.RiskLevel);
        var request = new ExecutionConfirmationRequest(
            id,
            plan.Id,
            representative.Task.Id,
            representative.Task.ToolName,
            representative.Tool?.Description ?? "Multiple planned actions require confirmation.",
            risk,
            representative.Decision.Reason,
            "Arguments are not displayed by default.",
            expiresAt,
            plan.Tasks.Count);
        var snapshot = Snapshot(plan);
        await pendingStore.CreateAsync(new PendingExecution(id, snapshot, representative.Task.Id, request, expiresAt, PendingExecutionStatus.PendingConfirmation), cancellationToken);
        if (snapshot.AuditContext is not null)
            _ = audit?.WriteAsync(new AuditEvent(snapshot.AuditContext.FlowId, timeProvider.GetUtcNow(), AuditEventType.PendingExecutionCreated, snapshot.Id, PendingExecutionId: id, Origin: snapshot.AuditContext.Origin, Status: PendingExecutionStatus.PendingConfirmation.ToString(), Risk: risk, ReasonCode: representative.Decision.Reason.ToString()), CancellationToken.None);
        return new(PendingExecutionStatus.PendingConfirmation, request);
    }

    public async Task<ExecutionConfirmationResult?> GetAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default)
    {
        var pending = await pendingStore.GetAsync(pendingExecutionId, cancellationToken);
        return pending is null ? null : new(pending.Status, pending.Confirmation, pending.ApprovalId, false);
    }

    public async Task<ExecutionConfirmationResult?> DecideAsync(Guid pendingExecutionId, ExecutionConfirmationDecision decision, CancellationToken cancellationToken = default)
    {
        var pending = await pendingStore.GetAsync(pendingExecutionId, cancellationToken);
        if (pending is null) return null;
        if (decision == ExecutionConfirmationDecision.Reject)
        {
            var rejected = pending with { Status = PendingExecutionStatus.Rejected };
            if (await pendingStore.TryTransitionAsync(pending.Id, PendingExecutionStatus.PendingConfirmation, rejected, cancellationToken))
            {
                if (pending.Plan.AuditContext is not null)
                    _ = audit?.WriteAsync(new AuditEvent(pending.Plan.AuditContext.FlowId, timeProvider.GetUtcNow(), AuditEventType.ExecutionRejected, pending.Plan.Id, PendingExecutionId: pending.Id, Origin: pending.Plan.AuditContext.Origin, Status: PendingExecutionStatus.Rejected.ToString()), CancellationToken.None);
                return new(PendingExecutionStatus.Rejected, pending.Confirmation);
            }
            return await GetAsync(pendingExecutionId, cancellationToken);
        }

        var approving = pending with { Status = PendingExecutionStatus.Executing };
        if (!await pendingStore.TryTransitionAsync(
                pending.Id,
                PendingExecutionStatus.PendingConfirmation,
                approving,
                cancellationToken))
        {
            return await GetAsync(pendingExecutionId, cancellationToken);
        }
        var approvalIds = new Dictionary<Guid, Guid>();
        foreach (var task in pending.Plan.Tasks)
        {
            var tool = tools.GetByName(task.ToolName);
            var policyDecision = policy.Evaluate(new ExecutionPolicyRequest(pending.Plan.Id, task.Id, task.ToolName, tool?.ExecutionMetadata));
            if (policyDecision.Type == ExecutionPolicyDecisionType.RequireConfirmation)
            {
                var approval = await approvals.CreateAsync(pending.Plan.Id, task.Id, ExecutionTaskFingerprint.Create(task), cancellationToken);
                approvalIds[task.Id] = approval.Id;
            }
        }
        var approved = pending with
        {
            Status = PendingExecutionStatus.Approved,
            ApprovalId = approvalIds.TryGetValue(pending.TaskId, out var approvalId) ? approvalId : null,
            ApprovalIds = approvalIds
        };
        await pendingStore.TryTransitionAsync(pending.Id, PendingExecutionStatus.Executing, approved, cancellationToken);
        if (pending.Plan.AuditContext is not null)
            _ = audit?.WriteAsync(new AuditEvent(pending.Plan.AuditContext.FlowId, timeProvider.GetUtcNow(), AuditEventType.ExecutionApproved, pending.Plan.Id, PendingExecutionId: pending.Id, ApprovalId: approved.ApprovalId, Origin: pending.Plan.AuditContext.Origin, Status: PendingExecutionStatus.Approved.ToString()), CancellationToken.None);
        return new(PendingExecutionStatus.Approved, pending.Confirmation, approved.ApprovalId);
    }

    public Task<PendingExecution?> TryTakeApprovedExecutionAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default) =>
        pendingStore.TryTakeApprovedAsync(pendingExecutionId, cancellationToken);

    private static Plan Snapshot(Plan plan) => plan with
    {
        Tasks = plan.Tasks.Select(task => task with
        {
            Arguments = task.Arguments.ToDictionary(item => item.Key, item => item.Value.Clone())
        }).ToArray()
    };
}
