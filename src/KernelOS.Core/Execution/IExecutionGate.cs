using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public interface IExecutionGate
{
    Task<ExecutionGateResult> EvaluateAsync(Guid planId, PlanTask task, Guid? approvalId, CancellationToken cancellationToken = default);
}
