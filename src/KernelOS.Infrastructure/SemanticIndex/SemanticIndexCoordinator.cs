using KernelOS.Core.SemanticIndex;
using KernelOS.Core.VectorIndex;
using KernelOS.Core.Memory;

namespace KernelOS.Infrastructure.SemanticIndex;

public sealed class SemanticIndexCoordinator : ISemanticIndexCoordinator, IMemoryMutationObserver
{
    private readonly object gate = new();
    private SemanticIndexStatus status = SemanticIndexStatus.NeedsRebuild;
    private long currentGeneration;
    private long appliedGeneration;
    private VectorFamilyKey? readyFamily;

    public SemanticIndexSnapshot GetSnapshot()
    {
        lock (gate) return new(status, currentGeneration, appliedGeneration, readyFamily);
    }

    public long RegisterMutation()
    {
        lock (gate)
        {
            currentGeneration++;
            if (status == SemanticIndexStatus.Ready) status = SemanticIndexStatus.Maintaining;
            return currentGeneration;
        }
    }

    public void MarkDirty()
    {
        lock (gate) status = SemanticIndexStatus.Dirty;
    }

    public SemanticRebuildContext BeginRebuild()
    {
        lock (gate)
        {
            var context = new SemanticRebuildContext(currentGeneration, status);
            status = SemanticIndexStatus.Building;
            return context;
        }
    }

    public void CompleteRebuild(SemanticRebuildContext context, VectorFamilyKey family)
    {
        lock (gate)
        {
            appliedGeneration = context.StartGeneration;
            readyFamily = family;
            status = currentGeneration == context.StartGeneration ? SemanticIndexStatus.Ready : SemanticIndexStatus.Dirty;
        }
    }

    public void AbortRebuild(SemanticRebuildContext context)
    {
        lock (gate)
        {
            if (status == SemanticIndexStatus.Building)
                status = currentGeneration == context.StartGeneration ? context.PreviousStatus : SemanticIndexStatus.Dirty;
        }
    }

    public bool CompleteIncremental(long generation, VectorFamilyKey family)
    {
        lock (gate)
        {
            if (status != SemanticIndexStatus.Maintaining || readyFamily is null || !readyFamily.Equals(family) || generation != appliedGeneration + 1)
            {
                status = SemanticIndexStatus.Dirty;
                return false;
            }
            appliedGeneration = generation;
            if (appliedGeneration == currentGeneration) status = SemanticIndexStatus.Ready;
            return true;
        }
    }

    public Task ObserveAsync(MemoryMutationCommitted mutation, CancellationToken cancellationToken = default)
    {
        RegisterMutation();
        return Task.CompletedTask;
    }
}
