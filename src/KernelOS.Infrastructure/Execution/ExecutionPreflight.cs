using KernelOS.Core.Execution;
using KernelOS.Core.Planning;

namespace KernelOS.Infrastructure.Execution;

public sealed class ExecutionPreflight(IExecutionGate gate) : IExecutionPreflight
{
    public async Task<ExecutionPreflightResult> EvaluateAsync(Plan plan, IReadOnlyDictionary<Guid, Guid>? approvalIds = null, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Guid, ExecutionGateResult>();
        var status = ExecutionGateStatus.Authorized;
        foreach (var task in plan.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var approvalId = approvalIds is not null && approvalIds.TryGetValue(task.Id, out var value) ? value : (Guid?)null;
            var result = await gate.EvaluateAsync(plan.Id, task, approvalId, consumeApproval: false, cancellationToken);
            results[task.Id] = result;
            if (result.Status == ExecutionGateStatus.Denied) status = ExecutionGateStatus.Denied;
            else if (result.Status == ExecutionGateStatus.RequiresConfirmation && status != ExecutionGateStatus.Denied) status = ExecutionGateStatus.RequiresConfirmation;
        }

        return new(status, results);
    }
}
