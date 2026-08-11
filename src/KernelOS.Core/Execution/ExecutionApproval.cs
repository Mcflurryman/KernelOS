namespace KernelOS.Core.Execution;

public sealed record ExecutionApproval(Guid Id, Guid PlanId, Guid TaskId, string TaskFingerprint, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
