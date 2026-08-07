namespace KernelOS.Core.Planning;
public interface IPlanBuilder { Task<PlannerResult> BuildAsync(Goal goal, CancellationToken cancellationToken = default); }
