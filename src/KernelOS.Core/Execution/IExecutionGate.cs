using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public interface IExecutionGate
{
    Task<ExecutionGateResult> EvaluateAsync(Guid planId, PlanTask task, Guid? approvalId, bool consumeApproval = true, CancellationToken cancellationToken = default);
}
