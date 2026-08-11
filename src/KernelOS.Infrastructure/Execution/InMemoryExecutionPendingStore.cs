using System.Collections.Concurrent;
using KernelOS.Core.Execution;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Execution;

public sealed class InMemoryExecutionPendingStore(IOptions<ExecutionPolicyOptions> options, TimeProvider timeProvider) : IExecutionPendingStore
{
    private readonly ConcurrentDictionary<Guid, PendingExecution> pendingExecutions = new();
    private readonly TimeSpan ttl = TimeSpan.FromMinutes(options.Value.ApprovalTtlMinutes);

    public Task<PendingExecution> CreateAsync(PendingExecution pendingExecution, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveExpired();
        if (!pendingExecutions.TryAdd(pendingExecution.Id, pendingExecution)) throw new InvalidOperationException("The pending execution could not be registered.");
        return Task.FromResult(pendingExecution);
    }

    public Task<PendingExecution?> GetAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetActive(pendingExecutionId));
    }

    public Task<bool> TryTransitionAsync(Guid pendingExecutionId, PendingExecutionStatus expected, PendingExecution updated, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = GetActive(pendingExecutionId);
        return Task.FromResult(current is not null && current.Status == expected && pendingExecutions.TryUpdate(pendingExecutionId, updated, current));
    }

    public Task<PendingExecution?> TryTakeApprovedAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = GetActive(pendingExecutionId);
        return Task.FromResult(current is not null && current.Status == PendingExecutionStatus.Approved && pendingExecutions.TryRemove(new KeyValuePair<Guid, PendingExecution>(pendingExecutionId, current)) ? current : null);
    }

    private PendingExecution? GetActive(Guid id)
    {
        if (!pendingExecutions.TryGetValue(id, out var pending)) return null;
        if (pending.ExpiresAt > timeProvider.GetUtcNow()) return pending;
        pendingExecutions.TryRemove(id, out _);
        return null;
    }

    private void RemoveExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in pendingExecutions.Where(item => item.Value.ExpiresAt <= now)) pendingExecutions.TryRemove(item.Key, out _);
    }
}
