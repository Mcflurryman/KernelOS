using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public interface IExecutionConfirmationService
{
    Task<ExecutionConfirmationResult> CreatePendingAsync(Plan plan, Guid taskId, CancellationToken cancellationToken = default);
    Task<ExecutionConfirmationResult?> GetAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default);
    Task<ExecutionConfirmationResult?> DecideAsync(Guid pendingExecutionId, ExecutionConfirmationDecision decision, CancellationToken cancellationToken = default);
    Task<PendingExecution?> TryTakeApprovedExecutionAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default);
}
