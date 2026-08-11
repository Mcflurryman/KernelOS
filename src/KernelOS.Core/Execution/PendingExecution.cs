using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public sealed record PendingExecution(Guid Id, Plan Plan, Guid TaskId, ExecutionConfirmationRequest Confirmation, DateTimeOffset ExpiresAt, PendingExecutionStatus Status, Guid? ApprovalId = null);
