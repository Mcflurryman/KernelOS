namespace KernelOS.Core.Execution;

public interface IExecutionApprovalStore
{
    Task<ExecutionApproval> CreateAsync(Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(Guid approvalId, Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default);
    Task<bool> TryConsumeAsync(Guid approvalId, Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default);
}
