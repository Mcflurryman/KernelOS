using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Audit;
using KernelOS.Core.Execution;
using KernelOS.Infrastructure.Execution;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Infrastructure;

public sealed class KernelPlanner(IPlanBuilder builder) : IPlanner
{
    public Task<PlannerResult> PlanAsync(Goal goal, CancellationToken cancellationToken = default) =>
        builder.BuildAsync(goal, cancellationToken);
}

public sealed class PlanBuilder(IExecutionAuditWriter? audit = null, TimeProvider? timeProvider = null) : IPlanBuilder
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

        var auditContext = goal.AuditContext ?? new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var plan = new Plan(Guid.NewGuid(), goal.Id, [task], PlannerStatus.Planned, null, null, auditContext);
        _ = audit?.WriteAsync(new AuditEvent(
                auditContext.FlowId,
                (timeProvider ?? TimeProvider.System).GetUtcNow(),
                AuditEventType.PlanCreated,
                plan.Id,
                Origin: auditContext.Origin,
                Status: PlannerStatus.Planned.ToString()), CancellationToken.None);
        return Task.FromResult(new PlannerResult(PlannerStatus.Planned, plan));
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

public sealed class PlanExecutor : IPlanExecutor
{
    private readonly IExecutionPreflight preflight;
    private readonly IExecutionGate executionGate;
    private readonly IToolRouter toolRouter;

    public PlanExecutor(IExecutionGate executionGate, IToolRouter toolRouter)
        : this(new Execution.ExecutionPreflight(executionGate), executionGate, toolRouter)
    {
    }

    public PlanExecutor(IExecutionPreflight preflight, IExecutionGate executionGate, IToolRouter toolRouter, IExecutionAuditWriter? audit = null, TimeProvider? timeProvider = null)
    {
        this.preflight = preflight;
        this.executionGate = executionGate;
        this.toolRouter = toolRouter;
        this.audit = audit;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }
    private readonly IExecutionAuditWriter? audit;
    private readonly TimeProvider timeProvider;
    public async Task<PlannerResult> ExecuteAsync(
        Plan plan,
        IReadOnlyDictionary<Guid, Guid>? approvalIds = null,
        CancellationToken cancellationToken = default)
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
        ExecutionPreflightResult authorization;
        try
        {
            authorization = await preflight.EvaluateAsync(plan, approvalIds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new(PlannerStatus.Cancelled, Cancel(plan, tasks));
        }

        if (authorization.Status != ExecutionGateStatus.Authorized)
        {
            var status = authorization.Status == ExecutionGateStatus.Denied ? PlannerStatus.Denied : PlannerStatus.RequiresConfirmation;
            var errorCode = status == PlannerStatus.Denied
                ? "execution_denied"
                : "confirmation_required";
            return new(
                status,
                plan with
                {
                    Tasks = tasks,
                    Status = status,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.UtcNow
                },
                new(errorCode, status.ToString()));
        }

        var context = plan.AuditContext;

        // Consume every scoped approval before the first Tool call, preventing partial execution on a race.
        foreach (var task in tasks)
        {
            if (approvalIds is null || !approvalIds.TryGetValue(task.Id, out var approvalId)) continue;
            var gate = await executionGate.EvaluateAsync(plan.Id, task, approvalId, cancellationToken: cancellationToken);
            if (gate.Status != ExecutionGateStatus.Authorized)
            {
                var status = gate.Status == ExecutionGateStatus.Denied ? PlannerStatus.Denied : PlannerStatus.RequiresConfirmation;
                return new(
                    status,
                    plan with
                    {
                        Tasks = tasks,
                        Status = status,
                        StartedAt = startedAt,
                        FinishedAt = DateTimeOffset.UtcNow
                    });
            }
        }

        var executionTimestamp = timeProvider.GetTimestamp();
        Write(AuditEventType.PlanExecutionStarted, plan, context, cancellationToken: CancellationToken.None);

        for (var taskIndex = 0; taskIndex < tasks.Count; taskIndex++)
        {
            var task = tasks[taskIndex];
            if (cancellationToken.IsCancellationRequested)
            {
                Write(AuditEventType.PlanExecutionCancelled, plan, context, duration: timeProvider.GetElapsedTime(executionTimestamp), cancellationToken: CancellationToken.None);
                return new(PlannerStatus.Cancelled, Cancel(plan, tasks, startedAt));
            }

            var taskTimestamp = timeProvider.GetTimestamp();
            try
            {
                Write(AuditEventType.TaskExecutionStarted, plan, context, task, cancellationToken: CancellationToken.None);
                var result = await toolRouter.ExecuteAsync(
                    new ToolExecutionRequest(task.ToolName, task.Arguments),
                    cancellationToken);
                var status = result.Status == ToolExecutionStatus.Success
                    ? PlannerStatus.Completed
                    : result.Status == ToolExecutionStatus.Cancelled
                        ? PlannerStatus.Cancelled
                        : PlannerStatus.Failed;
                tasks[taskIndex] = task with { Status = status, Result = result };
                var taskEvent = status == PlannerStatus.Completed ? AuditEventType.TaskExecutionCompleted : status == PlannerStatus.Cancelled ? AuditEventType.TaskExecutionCancelled : AuditEventType.TaskExecutionFailed;
                Write(taskEvent, plan, context, task, timeProvider.GetElapsedTime(taskTimestamp), CancellationToken.None);
                if (status != PlannerStatus.Completed)
                {
                    var error = status == PlannerStatus.Failed
                        ? new PlannerError("tool_failed", "The planned task did not complete.")
                        : null;
                    Write(status == PlannerStatus.Cancelled ? AuditEventType.PlanExecutionCancelled : AuditEventType.PlanExecutionFailed, plan, context, duration: timeProvider.GetElapsedTime(executionTimestamp), cancellationToken: CancellationToken.None);
                    return new(
                        status,
                        plan with
                        {
                            Tasks = tasks,
                            Status = status,
                            StartedAt = startedAt,
                            FinishedAt = DateTimeOffset.UtcNow
                        },
                        error);
                }
            }
            catch (OperationCanceledException)
            {
                Write(AuditEventType.TaskExecutionCancelled, plan, context, task, timeProvider.GetElapsedTime(taskTimestamp), CancellationToken.None);
                Write(AuditEventType.PlanExecutionCancelled, plan, context, duration: timeProvider.GetElapsedTime(executionTimestamp), cancellationToken: CancellationToken.None);
                return new(PlannerStatus.Cancelled, Cancel(plan, tasks, startedAt));
            }
            catch
            {
                Write(AuditEventType.TaskExecutionFailed, plan, context, task, timeProvider.GetElapsedTime(taskTimestamp), CancellationToken.None);
                Write(AuditEventType.PlanExecutionFailed, plan, context, duration: timeProvider.GetElapsedTime(executionTimestamp), cancellationToken: CancellationToken.None);
                return new(
                    PlannerStatus.Failed,
                    plan with
                    {
                        Tasks = tasks,
                        Status = PlannerStatus.Failed,
                        StartedAt = startedAt,
                        FinishedAt = DateTimeOffset.UtcNow
                    },
                    new("execution_failed", "The plan could not complete."));
            }
        }

        Write(AuditEventType.PlanExecutionCompleted, plan, context, duration: timeProvider.GetElapsedTime(executionTimestamp), cancellationToken: CancellationToken.None);
        return new(
            PlannerStatus.Completed,
            plan with
            {
                Tasks = tasks,
                Status = PlannerStatus.Completed,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow
            });
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

    private void Write(AuditEventType eventType, Plan plan, ExecutionAuditContext? context, PlanTask? task = null, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        if (context is not null)
            _ = audit?.WriteAsync(new AuditEvent(context.FlowId, timeProvider.GetUtcNow(), eventType, plan.Id, task?.Id, Origin: context.Origin, Duration: duration), cancellationToken);
    }
}
