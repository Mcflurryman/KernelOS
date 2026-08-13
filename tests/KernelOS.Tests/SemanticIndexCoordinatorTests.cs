using KernelOS.Core.SemanticIndex;
using KernelOS.Infrastructure.SemanticIndex;

namespace KernelOS.Tests;

public sealed class SemanticIndexCoordinatorTests
{
    [Fact]
    public void StartsNeedingRebuildAndTracksConcurrentMutations()
    {
        var coordinator = new SemanticIndexCoordinator();
        Parallel.For(0, 200, _ => coordinator.RegisterMutation());

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(SemanticIndexStatus.NeedsRebuild, snapshot.Status);
        Assert.Equal(200, snapshot.CurrentGeneration);
    }

    [Fact]
    public void SuccessfulRebuildOnlyBecomesReadyWhenGenerationIsUnchanged()
    {
        var coordinator = new SemanticIndexCoordinator();
        var start = coordinator.BeginRebuild();
        coordinator.CompleteRebuild(start, new("provider", "model", "1", 3));
        Assert.Equal(SemanticIndexStatus.Ready, coordinator.GetSnapshot().Status);

        var changedStart = coordinator.BeginRebuild();
        coordinator.RegisterMutation();
        coordinator.CompleteRebuild(changedStart, new("provider", "model", "1", 3));
        Assert.Equal(SemanticIndexStatus.Dirty, coordinator.GetSnapshot().Status);
    }

    [Fact]
    public void MutationAfterReadyEntersMaintainingUntilWorkerAppliesIt()
    {
        var coordinator = new SemanticIndexCoordinator();
        coordinator.CompleteRebuild(coordinator.BeginRebuild(), new("provider", "model", "1", 3));
        coordinator.RegisterMutation();
        coordinator.RegisterMutation();

        var snapshot = coordinator.GetSnapshot();
        Assert.Equal(SemanticIndexStatus.Maintaining, snapshot.Status);
        Assert.Equal(2, snapshot.CurrentGeneration);
        Assert.Equal(0, snapshot.AppliedGeneration);
    }
}
