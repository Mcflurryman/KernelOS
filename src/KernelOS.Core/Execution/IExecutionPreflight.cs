using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public interface IExecutionPreflight
{
    Task<ExecutionPreflightResult> EvaluateAsync(Plan plan, IReadOnlyDictionary<Guid, Guid>? approvalIds = null, CancellationToken cancellationToken = default);
}
