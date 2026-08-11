using System.Collections.Concurrent;
using KernelOS.Core.Execution;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Execution;

public sealed class InMemoryExecutionApprovalStore(IOptions<ExecutionPolicyOptions> options, TimeProvider timeProvider) : IExecutionApprovalStore
{
    private readonly ConcurrentDictionary<Guid, ExecutionApproval> approvals = new();
    private readonly TimeSpan approvalTtl = TimeSpan.FromMinutes(options.Value.ApprovalTtlMinutes);

    public Task<ExecutionApproval> CreateAsync(Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (planId == Guid.Empty || taskId == Guid.Empty || string.IsNullOrWhiteSpace(taskFingerprint))
        {
            throw new ArgumentException("Approval scope and task fingerprint are required.");
        }

        var createdAt = timeProvider.GetUtcNow();
        RemoveExpiredApprovals(createdAt);
        var approval = new ExecutionApproval(Guid.NewGuid(), planId, taskId, taskFingerprint, createdAt, createdAt.Add(approvalTtl));
        if (!approvals.TryAdd(approval.Id, approval))
        {
            throw new InvalidOperationException("The approval could not be registered.");
        }

        return Task.FromResult(approval);
    }

    public Task<bool> TryConsumeAsync(Guid approvalId, Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!approvals.TryGetValue(approvalId, out var approval)
            || approval.PlanId != planId
            || approval.TaskId != taskId
            || !string.Equals(approval.TaskFingerprint, taskFingerprint, StringComparison.Ordinal)
            || approval.ExpiresAt <= timeProvider.GetUtcNow())
        {
            if (approval is not null && approval.ExpiresAt <= timeProvider.GetUtcNow())
            {
                approvals.TryRemove(approvalId, out _);
            }

            return Task.FromResult(false);
        }

        return Task.FromResult(approvals.TryRemove(approvalId, out _));
    }

    public Task<bool> IsAvailableAsync(Guid approvalId, Guid planId, Guid taskId, string taskFingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var available = approvals.TryGetValue(approvalId, out var approval)
            && approval.PlanId == planId
            && approval.TaskId == taskId
            && string.Equals(approval.TaskFingerprint, taskFingerprint, StringComparison.Ordinal)
            && approval.ExpiresAt > timeProvider.GetUtcNow();
        return Task.FromResult(available);
    }

    private void RemoveExpiredApprovals(DateTimeOffset now)
    {
        foreach (var approval in approvals)
        {
            if (approval.Value.ExpiresAt <= now)
            {
                approvals.TryRemove(approval.Key, out _);
            }
        }
    }
}
