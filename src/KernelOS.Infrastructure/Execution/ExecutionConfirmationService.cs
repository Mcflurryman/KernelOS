using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionConfirmationService(
    IExecutionPolicy policy,
    IExecutionApprovalStore approvals,
    IExecutionPendingStore pendingStore,
    IToolRegistry tools,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<ExecutionPolicyOptions> options) : IExecutionConfirmationService
{
    private readonly TimeSpan ttl = TimeSpan.FromMinutes(options.Value.ApprovalTtlMinutes);

    public async Task<ExecutionConfirmationResult> CreatePendingAsync(Plan plan, Guid taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = plan?.Tasks.SingleOrDefault(item => item.Id == taskId);
        if (plan is null || plan.Id == Guid.Empty || plan.Tasks.Count != 1 || task is null || task.Status != PlannerStatus.Planned) return new(PendingExecutionStatus.NotConfirmable);

        var tool = tools.GetByName(task.ToolName);
        var decision = policy.Evaluate(new ExecutionPolicyRequest(plan.Id, task.Id, task.ToolName, tool?.ExecutionMetadata));
        if (decision.Type != ExecutionPolicyDecisionType.RequireConfirmation || decision.Reason == ExecutionPolicyReason.UnknownToolRequiresConfirmation) return new(PendingExecutionStatus.NotConfirmable);

        var id = Guid.NewGuid();
        var expiresAt = timeProvider.GetUtcNow().Add(ttl);
        var request = new ExecutionConfirmationRequest(id, plan.Id, task.Id, task.ToolName, tool?.Description ?? "", decision.RiskLevel, decision.Reason, "Arguments are not displayed by default.", expiresAt);
        var snapshot = Snapshot(plan);
        await pendingStore.CreateAsync(new PendingExecution(id, snapshot, task.Id, request, expiresAt, PendingExecutionStatus.PendingConfirmation), cancellationToken);
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
            return await pendingStore.TryTransitionAsync(pending.Id, PendingExecutionStatus.PendingConfirmation, rejected, cancellationToken)
                ? new(PendingExecutionStatus.Rejected, pending.Confirmation)
                : await GetAsync(pendingExecutionId, cancellationToken);
        }

        var approving = pending with { Status = PendingExecutionStatus.Executing };
        if (!await pendingStore.TryTransitionAsync(pending.Id, PendingExecutionStatus.PendingConfirmation, approving, cancellationToken)) return await GetAsync(pendingExecutionId, cancellationToken);
        var task = pending.Plan.Tasks.Single(item => item.Id == pending.TaskId);
        var approval = await approvals.CreateAsync(pending.Plan.Id, task.Id, ExecutionTaskFingerprint.Create(task), cancellationToken);
        var approved = pending with { Status = PendingExecutionStatus.Approved, ApprovalId = approval.Id };
        await pendingStore.TryTransitionAsync(pending.Id, PendingExecutionStatus.Executing, approved, cancellationToken);
        return new(PendingExecutionStatus.Approved, pending.Confirmation, approval.Id);
    }

    public Task<PendingExecution?> TryTakeApprovedExecutionAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default) =>
        pendingStore.TryTakeApprovedAsync(pendingExecutionId, cancellationToken);

    private static Plan Snapshot(Plan plan) => plan with { Tasks = plan.Tasks.Select(task => task with { Arguments = task.Arguments.ToDictionary(item => item.Key, item => item.Value.Clone()) }).ToArray() };
}
