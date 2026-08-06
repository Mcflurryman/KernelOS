namespace KernelOS.Core.Planning;
public interface IPlanner { Task<PlannerResult> PlanAsync(Goal goal, CancellationToken cancellationToken = default); }
