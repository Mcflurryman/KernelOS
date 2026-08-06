namespace KernelOS.Core.Planning;
public sealed record PlannerResult(PlannerStatus Status, Plan? Plan, PlannerError? Error = null);
