using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Tools;
using Microsoft.Extensions.Logging;
namespace KernelOS.Infrastructure;
public sealed class KernelPlanner(IToolRouter toolRouter, ILogger<KernelPlanner> logger) : IPlanner
{
    public async Task<PlannerResult> PlanAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(PlannerStatus.Cancelled, null);
        if (goal is null || string.IsNullOrWhiteSpace(goal.UserRequest)) return new(PlannerStatus.Failed, null, new("invalid_goal", "A user request is required."));
        if (!goal.UserRequest.StartsWith("EJECUTAR", StringComparison.OrdinalIgnoreCase) || !TryCreateTask(goal, out var task))
            return new(PlannerStatus.Failed, null, new("unsupported_goal", "The goal cannot be planned by the current planner."));
        var started = DateTimeOffset.UtcNow;
        var plan = new Plan(Guid.NewGuid(), goal.Id, [task], PlannerStatus.Executing, started, null);
        try
        {
            var result = await toolRouter.ExecuteAsync(new ToolExecutionRequest(task.ToolName, task.Arguments), cancellationToken);
            var status = result.Status == ToolExecutionStatus.Success ? PlannerStatus.Completed : result.Status == ToolExecutionStatus.Cancelled ? PlannerStatus.Cancelled : PlannerStatus.Failed;
            var completedTask = task with { Status = status, Result = result };
            var completedPlan = plan with { Tasks = [completedTask], Status = status, FinishedAt = DateTimeOffset.UtcNow };
            return status == PlannerStatus.Failed ? new(status, completedPlan, new("tool_failed", "The planned task did not complete.")) : new(status, completedPlan);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(PlannerStatus.Cancelled, plan with { Status = PlannerStatus.Cancelled, FinishedAt = DateTimeOffset.UtcNow }); }
        catch (Exception exception) { PlannerLog.Failed(logger, exception); return new(PlannerStatus.Failed, plan with { Status = PlannerStatus.Failed, FinishedAt = DateTimeOffset.UtcNow }, new("execution_failed", "The plan could not complete.")); }
    }
    private static bool TryCreateTask(Goal goal, out PlanTask task)
    {
        task = default!;
        if (goal.Metadata is null || !goal.Metadata.TryGetValue("tool", out var tool) || tool.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tool.GetString()) || !goal.Metadata.TryGetValue("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object) return false;
        var values = arguments.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone());
        task = new(Guid.NewGuid(), "Execute requested tool", tool.GetString()!, values, PlannerStatus.Planning, 0);
        return true;
    }
}
internal static partial class PlannerLog { [LoggerMessage(EventId = 30, Level = LogLevel.Error, Message = "Planner execution failed.")] public static partial void Failed(ILogger logger, Exception exception); }
