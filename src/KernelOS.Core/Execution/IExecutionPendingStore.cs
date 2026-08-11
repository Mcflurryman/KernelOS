namespace KernelOS.Core.Execution;

public interface IExecutionPendingStore
{
    Task<PendingExecution> CreateAsync(PendingExecution pendingExecution, CancellationToken cancellationToken = default);
    Task<PendingExecution?> GetAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default);
    Task<bool> TryTransitionAsync(Guid pendingExecutionId, PendingExecutionStatus expected, PendingExecution updated, CancellationToken cancellationToken = default);
    Task<PendingExecution?> TryTakeApprovedAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default);
}
