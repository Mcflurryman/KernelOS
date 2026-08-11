using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionGate(IExecutionPolicy policy, IExecutionApprovalStore approvals, IToolRegistry tools) : IExecutionGate
{
    public async Task<ExecutionGateResult> EvaluateAsync(Guid planId, PlanTask task, Guid? approvalId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = tools.GetByName(task.ToolName)?.ExecutionMetadata;
        var decision = policy.Evaluate(new ExecutionPolicyRequest(planId, task.Id, task.ToolName, metadata));
        if (decision.Type == ExecutionPolicyDecisionType.Allow)
        {
            return new(ExecutionGateStatus.Authorized, decision);
        }

        if (decision.Type == ExecutionPolicyDecisionType.Deny)
        {
            return new(ExecutionGateStatus.Denied, decision);
        }

        if (decision.Reason == ExecutionPolicyReason.UnknownToolRequiresConfirmation)
        {
            return new(ExecutionGateStatus.RequiresConfirmation, decision);
        }

        if (approvalId is null)
        {
            return new(ExecutionGateStatus.RequiresConfirmation, decision);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var consumed = await approvals.TryConsumeAsync(approvalId.Value, planId, task.Id, ExecutionTaskFingerprint.Create(task), cancellationToken);
        return consumed
            ? new(ExecutionGateStatus.Authorized, decision, approvalId)
            : new(ExecutionGateStatus.RequiresConfirmation, decision);
    }
}
