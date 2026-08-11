using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Infrastructure;

public sealed class KernelPlanner(IPlanBuilder builder) : IPlanner
{
    public Task<PlannerResult> PlanAsync(Goal goal, CancellationToken cancellationToken = default) =>
        builder.BuildAsync(goal, cancellationToken);
}

public sealed class PlanBuilder : IPlanBuilder
{
    public Task<PlannerResult> BuildAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new PlannerResult(PlannerStatus.Cancelled, null));
        }

        if (goal is null || goal.Id == Guid.Empty || string.IsNullOrWhiteSpace(goal.UserRequest))
        {
            return Task.FromResult(new PlannerResult(PlannerStatus.Failed, null, new("invalid_goal", "A goal identifier and user request are required.")));
        }

        if (!TryCreateTask(goal, out var task))
        {
            return Task.FromResult(new PlannerResult(PlannerStatus.Failed, null, new("unsupported_goal", "The goal cannot be planned.")));
        }

        return Task.FromResult(new PlannerResult(PlannerStatus.Planned, new Plan(Guid.NewGuid(), goal.Id, [task], PlannerStatus.Planned, null, null)));
    }

    private static bool TryCreateTask(Goal goal, out PlanTask task)
    {
        task = new PlanTask(
            Guid.Empty,
            string.Empty,
            string.Empty,
            new Dictionary<string, JsonElement>(),
            PlannerStatus.Failed,
            0);
        if (!goal.UserRequest.StartsWith("EJECUTAR", StringComparison.OrdinalIgnoreCase)
            || goal.Metadata is null
            || !goal.Metadata.TryGetValue("tool", out var tool)
            || tool.ValueKind != JsonValueKind.String
            || !goal.Metadata.TryGetValue("arguments", out var arguments)
            || arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var toolName = tool.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        task = new PlanTask(
            Guid.NewGuid(),
            "Execute requested tool",
            toolName,
            arguments.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone()),
            PlannerStatus.Planned,
            0);
        return true;
    }
}

public sealed class PlanExecutor(IExecutionGate executionGate, IToolRouter toolRouter) : IPlanExecutor
{
    public async Task<PlannerResult> ExecuteAsync(Plan plan, IReadOnlyDictionary<Guid, Guid>? approvalIds = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(PlannerStatus.Cancelled, plan is null ? null : Cancel(plan, []));
        }

        if (!IsValidForExecution(plan))
        {
            return new(PlannerStatus.Failed, null, new("invalid_plan", "The plan cannot be executed."));
        }

        var startedAt = DateTimeOffset.UtcNow;
        var tasks = plan.Tasks.ToList();
        for (var taskIndex = 0; taskIndex < tasks.Count; taskIndex++)
        {
            var task = tasks[taskIndex];
            if (cancellationToken.IsCancellationRequested)
            {
                return new(PlannerStatus.Cancelled, Cancel(plan, tasks, startedAt));
            }

            try
            {
                var approvalId = approvalIds is not null && approvalIds.TryGetValue(task.Id, out var value) ? value : (Guid?)null;
                var gate = await executionGate.EvaluateAsync(plan.Id, task, approvalId, cancellationToken);
                if (gate.Status != ExecutionGateStatus.Authorized)
                {
                    var gatePlannerStatus = gate.Status == ExecutionGateStatus.RequiresConfirmation ? PlannerStatus.RequiresConfirmation : PlannerStatus.Denied;
                    var errorCode = gate.Status == ExecutionGateStatus.RequiresConfirmation ? "confirmation_required" : "execution_denied";
                    tasks[taskIndex] = task with { Status = gatePlannerStatus };
                    return new(gatePlannerStatus, plan with { Tasks = tasks, Status = gatePlannerStatus, StartedAt = startedAt, FinishedAt = DateTimeOffset.UtcNow }, new(errorCode, gate.Decision.Reason.ToString()));
                }

                var result = await toolRouter.ExecuteAsync(new ToolExecutionRequest(task.ToolName, task.Arguments), cancellationToken);
                var status = result.Status == ToolExecutionStatus.Success ? PlannerStatus.Completed : result.Status == ToolExecutionStatus.Cancelled ? PlannerStatus.Cancelled : PlannerStatus.Failed;
                tasks[taskIndex] = task with { Status = status, Result = result };
                if (status != PlannerStatus.Completed)
                {
                    return new(status, plan with { Tasks = tasks, Status = status, StartedAt = startedAt, FinishedAt = DateTimeOffset.UtcNow }, status == PlannerStatus.Failed ? new("tool_failed", "The planned task did not complete.") : null);
                }
            }
            catch (OperationCanceledException)
            {
                return new(PlannerStatus.Cancelled, Cancel(plan, tasks, startedAt));
            }
            catch
            {
                return new(PlannerStatus.Failed, plan with { Tasks = tasks, Status = PlannerStatus.Failed, StartedAt = startedAt, FinishedAt = DateTimeOffset.UtcNow }, new("execution_failed", "The plan could not complete."));
            }
        }

        return new(PlannerStatus.Completed, plan with { Tasks = tasks, Status = PlannerStatus.Completed, StartedAt = startedAt, FinishedAt = DateTimeOffset.UtcNow });
    }

    private static bool IsValidForExecution(Plan? plan) =>
        plan is not null
        && plan.Id != Guid.Empty
        && plan.GoalId != Guid.Empty
        && plan.Status == PlannerStatus.Planned
        && plan.StartedAt is null
        && plan.FinishedAt is null
        && plan.Tasks is { Count: > 0 }
        && plan.Tasks.All(task => task is not null
            && task.Id != Guid.Empty
            && !string.IsNullOrWhiteSpace(task.Name)
            && !string.IsNullOrWhiteSpace(task.ToolName)
            && task.Arguments is not null
            && task.Status == PlannerStatus.Planned
            && task.RetryCount >= 0
            && task.Result is null);

    private static Plan Cancel(Plan plan, IReadOnlyCollection<PlanTask> tasks, DateTimeOffset? startedAt = null) =>
        plan with
        {
            Tasks = tasks,
            Status = PlannerStatus.Cancelled,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow
        };
}
