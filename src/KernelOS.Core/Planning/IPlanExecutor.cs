namespace KernelOS.Core.Planning;
public interface IPlanExecutor { Task<PlannerResult> ExecuteAsync(Plan plan, CancellationToken cancellationToken = default); }
