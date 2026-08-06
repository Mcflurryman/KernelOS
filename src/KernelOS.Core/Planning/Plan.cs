namespace KernelOS.Core.Planning;
public sealed record Plan(Guid Id, Guid GoalId, IReadOnlyCollection<PlanTask> Tasks, PlannerStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);
