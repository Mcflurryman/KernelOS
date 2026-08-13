using KernelOS.Core.Memory;

namespace KernelOS.Infrastructure.Memory;

internal sealed class NullMemoryMutationObserver : IMemoryMutationObserver
{
    public static NullMemoryMutationObserver Instance { get; } = new();

    public Task ObserveAsync(MemoryMutationCommitted mutation, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
