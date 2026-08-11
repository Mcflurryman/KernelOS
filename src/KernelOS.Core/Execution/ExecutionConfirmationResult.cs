namespace KernelOS.Core.Execution;

public sealed record ExecutionConfirmationResult(PendingExecutionStatus Status, ExecutionConfirmationRequest? Confirmation = null, Guid? ApprovalId = null, bool Transitioned = true);
