using KernelOS.Core.Audit;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Core.Kai;
using KernelOS.Core.Conversation;
using KernelOS.Core.Rag;
using KernelOS.Infrastructure;
using KernelOS.Infrastructure.Kai;
using KernelOS.Infrastructure.Execution;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace KernelOS.Tests;

public sealed class ExecutionAuditTrailTests
{
    [Fact]
    public async Task WriteAsyncPreservesInsertionOrder()
    {
        var trail = Trail(3);
        var flow = AuditFlowId.Create();
        var first = Event(flow, AuditEventType.PlanCreated);
        var second = Event(flow, AuditEventType.PreflightCompleted);

        await trail.WriteAsync(first);
        await trail.WriteAsync(second);

        Assert.Equal(new[] { first, second }, await trail.GetSnapshotAsync());
    }

    [Fact]
    public async Task WriteAsyncEvictsOldestEventWhenCapacityIsExceeded()
    {
        var trail = Trail(1);
        var first = Event(AuditFlowId.Create(), AuditEventType.PlanCreated);
        var second = Event(AuditFlowId.Create(), AuditEventType.PlanExecutionCompleted);

        await trail.WriteAsync(first);
        await trail.WriteAsync(second);

        Assert.Equal(new[] { second }, await trail.GetSnapshotAsync());
    }

    [Fact]
    public async Task SnapshotIsIndependentFromLaterWrites()
    {
        var trail = Trail(2);
        var first = Event(AuditFlowId.Create(), AuditEventType.PlanCreated);
        await trail.WriteAsync(first);
        var snapshot = await trail.GetSnapshotAsync();
        await trail.WriteAsync(Event(AuditFlowId.Create(), AuditEventType.PlanExecutionCompleted));
        Assert.Equal(new[] { first }, snapshot);
    }

    [Fact]
    public async Task ConcurrentWritesRetainIndependentFlowsWithinCapacity()
    {
        var trail = Trail(64);
        var flows = Enumerable.Range(0, 32).Select(_ => AuditFlowId.Create()).ToArray();
        await Task.WhenAll(flows.Select(flow => trail.WriteAsync(Event(flow, AuditEventType.PlanCreated))));
        var snapshot = await trail.GetSnapshotAsync();
        Assert.Equal(flows.OrderBy(flow => flow.Value), snapshot.Select(item => item.FlowId).OrderBy(flow => flow.Value));
    }

    [Fact]
    public async Task CancellationPreventsWrite()
    {
        var trail = Trail(1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => trail.WriteAsync(Event(AuditFlowId.Create(), AuditEventType.PlanCreated), cancellation.Token));
        Assert.Empty(await trail.GetSnapshotAsync());
    }

    [Fact]
    public void AuditEventHasOnlySafeTypedMetadata()
    {
        var names = typeof(AuditEvent).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain("Message", names);
        Assert.DoesNotContain("Arguments", names);
        Assert.DoesNotContain("Result", names);
        Assert.DoesNotContain("Content", names);
        Assert.DoesNotContain("Secret", names);
    }

    [Fact]
    public void StoreRejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Trail(0));
    }

    [Fact]
    public void AuditFlowIdIsGeneratedIndependently()
    {
        Assert.NotEqual(AuditFlowId.Create(), AuditFlowId.Create());
    }

    [Fact]
    public async Task PlanBuilderCreatesPlannerFlowWhenGoalHasNoAuditContext()
    {
        var trail = Trail(10);
        var goal = new Goal(Guid.NewGuid(), "EJECUTAR", DateTimeOffset.UtcNow, 0,
            new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement("read"), ["arguments"] = JsonSerializer.SerializeToElement(new { }) });

        var result = await new PlanBuilder(Writer(trail), new TestTimeProvider()).BuildAsync(goal);
        var auditEvent = Assert.Single(await trail.GetSnapshotAsync());

        Assert.NotNull(result.Plan!.AuditContext);
        Assert.Equal(ExecutionOrigin.Planner, result.Plan.AuditContext.Origin);
        Assert.Equal(result.Plan.AuditContext.FlowId, auditEvent.FlowId);
        Assert.Equal(ExecutionOrigin.Planner, auditEvent.Origin);
    }

    [Fact]
    public async Task PlanCreatedPreservesGoalContextWithoutLeakingGoalOrArguments()
    {
        var trail = Trail(10);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Kai);
        var goal = new Goal(Guid.NewGuid(), "EJECUTAR SUPER_SECRET_CHAT_AUDIT", DateTimeOffset.UtcNow, 0,
            new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement("echo"), ["arguments"] = JsonSerializer.SerializeToElement(new { value = "SUPER_SECRET_TOOL_ARGUMENT" }) }, context);
        var result = await new PlanBuilder(Writer(trail), new TestTimeProvider()).BuildAsync(goal);
        var auditEvent = Assert.Single(await trail.GetSnapshotAsync());
        Assert.Equal(AuditEventType.PlanCreated, auditEvent.EventType);
        Assert.Equal(context.FlowId, auditEvent.FlowId);
        Assert.Equal(result.Plan!.Id, auditEvent.PlanId);
        Assert.Equal(ExecutionOrigin.Kai, auditEvent.Origin);
        Assert.DoesNotContain("SUPER_SECRET", JsonSerializer.Serialize(await trail.GetSnapshotAsync()));
    }

    [Theory]
    [InlineData("read", ExecutionGateStatus.Authorized)]
    [InlineData("write", ExecutionGateStatus.RequiresConfirmation)]
    [InlineData("blocked", ExecutionGateStatus.Denied)]
    public async Task PreflightWritesActualTaskAndAggregateDecision(string toolName, ExecutionGateStatus expected)
    {
        var trail = Trail(10);
        var task = new PlanTask(Guid.NewGuid(), "task", toolName, new Dictionary<string, JsonElement> { ["secret"] = JsonSerializer.SerializeToElement("SUPER_SECRET_TOOL_ARGUMENT") }, PlannerStatus.Planned, 0);
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [task], PlannerStatus.Planned, null, null, new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner));
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)), new TestTool("write", new(false, true, false, ExecutionRiskLevel.High)), new TestTool("blocked", new(false, true, true, ExecutionRiskLevel.Critical)));
        var gate = new ExecutionGate(new DefaultExecutionPolicy(), Store(), tools);
        var result = await new ExecutionPreflight(gate, Writer(trail), new TestTimeProvider()).EvaluateAsync(plan);
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(new[] { AuditEventType.PreflightStarted, AuditEventType.TaskAuthorizationEvaluated, AuditEventType.PreflightCompleted }, events.Select(item => item.EventType));
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected.ToString(), events[^1].Status);
        Assert.DoesNotContain("SUPER_SECRET", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task PreflightRecordsAllTasksAndDenyPrecedence()
    {
        var trail = Trail(10);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var first = new PlanTask(Guid.NewGuid(), "first", "write", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
        var second = new PlanTask(Guid.NewGuid(), "second", "blocked", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [first, second], PlannerStatus.Planned, null, null, context);
        var tools = new TestRegistry(new TestTool("write", new(false, true, false, ExecutionRiskLevel.High)), new TestTool("blocked", new(false, true, true, ExecutionRiskLevel.Critical)));
        var result = await new ExecutionPreflight(new ExecutionGate(new DefaultExecutionPolicy(), Store(), tools), Writer(trail), new TestTimeProvider()).EvaluateAsync(plan);
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(ExecutionGateStatus.Denied, result.Status);
        Assert.Equal(2, events.Count(item => item.EventType == AuditEventType.TaskAuthorizationEvaluated));
        Assert.Equal(ExecutionGateStatus.Denied.ToString(), events[^1].Status);
    }

    [Fact]
    public async Task PreflightRecordsMultiTaskConfirmationInTaskOrder()
    {
        var trail = Trail(10);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var tasks = new[]
        {
            new PlanTask(Guid.NewGuid(), "first", "read", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0),
            new PlanTask(Guid.NewGuid(), "second", "write", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0),
            new PlanTask(Guid.NewGuid(), "third", "read", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0)
        };
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)), new TestTool("write", new(false, true, false, ExecutionRiskLevel.High)));
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), tasks, PlannerStatus.Planned, null, null, context);
        var result = await new ExecutionPreflight(new ExecutionGate(new DefaultExecutionPolicy(), Store(), tools), Writer(trail), new TestTimeProvider()).EvaluateAsync(plan);
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(ExecutionGateStatus.RequiresConfirmation, result.Status);
        Assert.Equal(new[] { ExecutionPolicyDecisionType.Allow, ExecutionPolicyDecisionType.RequireConfirmation, ExecutionPolicyDecisionType.Allow }, events.Where(item => item.EventType == AuditEventType.TaskAuthorizationEvaluated).Select(item => item.PolicyDecision!.Value).ToArray());
        Assert.Equal(ExecutionGateStatus.RequiresConfirmation.ToString(), events[^1].Status);
    }

    [Fact]
    public async Task AuditFailuresDoNotChangePlanBuilderOrPreflightResults()
    {
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var goal = new Goal(Guid.NewGuid(), "EJECUTAR", DateTimeOffset.UtcNow, 0, new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement("read"), ["arguments"] = JsonSerializer.SerializeToElement(new { }) }, context);
        var builder = new PlanBuilder(Writer(new ThrowingTrail()), new TestTimeProvider());
        var built = await builder.BuildAsync(goal);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var preflight = new ExecutionPreflight(new ExecutionGate(new DefaultExecutionPolicy(), Store(), tools), Writer(new ThrowingTrail()), new TestTimeProvider());
        var preflightResult = await preflight.EvaluateAsync(built.Plan!);
        Assert.Equal(PlannerStatus.Planned, built.Status);
        Assert.Equal(ExecutionGateStatus.Authorized, preflightResult.Status);
    }

    [Fact]
    public async Task KaiPlannerFlowUsesOneOrderedAuditFlowWithoutArgumentLeakage()
    {
        var trail = Trail(32);
        var agent = Agent(trail);
        var response = await agent.HandleAsync(new KaiRequest("ignored", PreferredMode: KaiMode.Planner, ToolName: "read", Arguments: new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement("SUPER_SECRET_TOOL_ARGUMENT") }));
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(KaiStatus.Completed, response.Status);
        Assert.Single(events.Select(item => item.FlowId).Distinct());
        Assert.Equal(new[] { AuditEventType.KaiRequestStarted, AuditEventType.KaiRouteSelected, AuditEventType.PlanCreated, AuditEventType.PreflightStarted, AuditEventType.TaskAuthorizationEvaluated, AuditEventType.PreflightCompleted }, events.Select(item => item.EventType));
        Assert.All(events.Where(item => item.PlanId is not null), item => Assert.Equal(response.PlanId, item.PlanId));
        Assert.DoesNotContain("SUPER_SECRET_TOOL_ARGUMENT", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task KaiChatAndRagAuditDoNotContainUserContentAndUseSeparateFlows()
    {
        var trail = Trail(32);
        var agent = Agent(trail);
        await Task.WhenAll(
            agent.HandleAsync(new KaiRequest("SUPER_SECRET_CHAT_AUDIT", PreferredMode: KaiMode.Chat)),
            agent.HandleAsync(new KaiRequest("SUPER_SECRET_RAG_AUDIT", PreferredMode: KaiMode.Rag)));
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(2, events.Select(item => item.FlowId).Distinct().Count());
        Assert.DoesNotContain("SUPER_SECRET_CHAT_AUDIT", JsonSerializer.Serialize(events));
        Assert.DoesNotContain("SUPER_SECRET_RAG_AUDIT", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task KaiContinuesWhenAuditSinkFails()
    {
        var agent = Agent(new ThrowingTrail());
        var result = await agent.HandleAsync(new KaiRequest("hello", PreferredMode: KaiMode.Chat));
        Assert.Equal(KaiStatus.Success, result.Status);
        Assert.Equal(KaiMode.Chat, result.ModeUsed);
    }

    [Theory]
    [InlineData(ExecutionConfirmationDecision.Approve, AuditEventType.ExecutionApproved)]
    [InlineData(ExecutionConfirmationDecision.Reject, AuditEventType.ExecutionRejected)]
    public async Task ConfirmationEventsPreservePendingFlowAndDoNotExecute(ExecutionConfirmationDecision decision, AuditEventType terminalEvent)
    {
        var trail = Trail(10);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var task = new PlanTask(Guid.NewGuid(), "task", "write", new Dictionary<string, JsonElement> { ["secret"] = JsonSerializer.SerializeToElement("SUPER_SECRET_CONFIRM_ARGUMENT") }, PlannerStatus.Planned, 0);
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [task], PlannerStatus.Planned, null, null, context);
        var service = ConfirmationService(trail);
        var pending = await service.CreatePendingAsync(plan, task.Id);
        var result = await service.DecideAsync(pending.Confirmation!.PendingExecutionId, decision);
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(new[] { AuditEventType.PendingExecutionCreated, terminalEvent }, events.Select(item => item.EventType));
        Assert.All(events, item => { Assert.Equal(context.FlowId, item.FlowId); Assert.Equal(plan.Id, item.PlanId); Assert.Equal(pending.Confirmation.PendingExecutionId, item.PendingExecutionId); });
        Assert.DoesNotContain(events, item => item.EventType is AuditEventType.PlanExecutionStarted or AuditEventType.TaskExecutionStarted);
        Assert.DoesNotContain("SUPER_SECRET_CONFIRM_ARGUMENT", JsonSerializer.Serialize(events));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ConfirmationServiceContinuesWhenAuditSinkFails()
    {
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var task = new PlanTask(Guid.NewGuid(), "task", "write", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [task], PlannerStatus.Planned, null, null, context);
        var service = ConfirmationService(new ThrowingTrail());
        var pending = await service.CreatePendingAsync(plan, task.Id);
        var approved = await service.DecideAsync(pending.Confirmation!.PendingExecutionId, ExecutionConfirmationDecision.Approve);
        Assert.Equal(PendingExecutionStatus.PendingConfirmation, pending.Status);
        Assert.Equal(PendingExecutionStatus.Approved, approved!.Status);
        var rejectedPlan = plan with { Id = Guid.NewGuid(), AuditContext = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner) };
        var rejectedPending = await service.CreatePendingAsync(rejectedPlan, task.Id);
        Assert.Equal(PendingExecutionStatus.Rejected, (await service.DecideAsync(rejectedPending.Confirmation!.PendingExecutionId, ExecutionConfirmationDecision.Reject))!.Status);
    }

    [Fact]
    public async Task IndependentPendingsKeepAuditFlowsSeparated()
    {
        var trail = Trail(10);
        var service = ConfirmationService(trail);
        var taskA = new PlanTask(Guid.NewGuid(), "a", "write", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
        var taskB = new PlanTask(Guid.NewGuid(), "b", "write", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
        var planA = new Plan(Guid.NewGuid(), Guid.NewGuid(), [taskA], PlannerStatus.Planned, null, null, new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner));
        var planB = new Plan(Guid.NewGuid(), Guid.NewGuid(), [taskB], PlannerStatus.Planned, null, null, new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner));
        var pendingA = await service.CreatePendingAsync(planA, taskA.Id);
        var pendingB = await service.CreatePendingAsync(planB, taskB.Id);
        await service.DecideAsync(pendingA.Confirmation!.PendingExecutionId, ExecutionConfirmationDecision.Approve);
        await service.DecideAsync(pendingB.Confirmation!.PendingExecutionId, ExecutionConfirmationDecision.Reject);
        var events = await trail.GetSnapshotAsync();
        Assert.NotEqual(pendingA.Confirmation.PendingExecutionId, pendingB.Confirmation.PendingExecutionId);
        Assert.NotEqual(planA.AuditContext!.FlowId, planB.AuditContext!.FlowId);
        Assert.All(events.Where(item => item.PendingExecutionId == pendingA.Confirmation.PendingExecutionId), item => Assert.Equal(planA.AuditContext.FlowId, item.FlowId));
        Assert.All(events.Where(item => item.PendingExecutionId == pendingB.Confirmation.PendingExecutionId), item => Assert.Equal(planB.AuditContext.FlowId, item.FlowId));
    }

    [Theory]
    [InlineData(ToolExecutionStatus.Success, PlannerStatus.Completed, AuditEventType.TaskExecutionCompleted, AuditEventType.PlanExecutionCompleted)]
    [InlineData(ToolExecutionStatus.Failure, PlannerStatus.Failed, AuditEventType.TaskExecutionFailed, AuditEventType.PlanExecutionFailed)]
    public async Task ExecutorWritesSafeTerminalEvents(ToolExecutionStatus toolStatus, PlannerStatus expectedStatus, AuditEventType taskTerminal, AuditEventType planTerminal)
    {
        var trail = Trail(10);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var task = new PlanTask(Guid.NewGuid(), "task", "read", new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement("SUPER_SECRET_EXEC_ARGUMENT") }, PlannerStatus.Planned, 0);
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [task], PlannerStatus.Planned, null, null, context);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var executor = new PlanExecutor(new ExecutionPreflight(new ExecutionGate(new DefaultExecutionPolicy(), store, tools)), new ExecutionGate(new DefaultExecutionPolicy(), store, tools), new FakeRouter(new ToolExecutionResult(toolStatus, "SUPER_SECRET_EXEC_RESULT")), Writer(trail), new TestTimeProvider());
        var result = await executor.ExecuteAsync(plan);
        var events = await trail.GetSnapshotAsync();
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(new[] { AuditEventType.PlanExecutionStarted, AuditEventType.TaskExecutionStarted, taskTerminal, planTerminal }, events.Select(item => item.EventType));
        Assert.All(events, item => Assert.Equal(context.FlowId, item.FlowId));
        Assert.DoesNotContain("SUPER_SECRET_EXEC", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task ExecutorAuditsEveryTaskOfSuccessfulMultiTaskPlanInOrder()
    {
        var trail = Trail(16);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var tasks = new[]
        {
            CreateTask("first"),
            CreateTask("second"),
            CreateTask("third")
        };
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), tasks, PlannerStatus.Planned, null, null, context);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var gate = new ExecutionGate(new DefaultExecutionPolicy(), store, tools);
        var executor = new PlanExecutor(new ExecutionPreflight(gate), gate, new FakeRouter(ToolExecutionResult.Success("ok")), Writer(trail), new TestTimeProvider());

        var result = await executor.ExecuteAsync(plan);
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(PlannerStatus.Completed, result.Status);
        Assert.Equal(new[]
        {
            AuditEventType.PlanExecutionStarted,
            AuditEventType.TaskExecutionStarted, AuditEventType.TaskExecutionCompleted,
            AuditEventType.TaskExecutionStarted, AuditEventType.TaskExecutionCompleted,
            AuditEventType.TaskExecutionStarted, AuditEventType.TaskExecutionCompleted,
            AuditEventType.PlanExecutionCompleted
        }, events.Select(item => item.EventType));
        Assert.All(events, item => Assert.Equal(context.FlowId, item.FlowId));
        Assert.Equal(tasks.SelectMany(task => new Guid?[] { task.Id, task.Id }), events.Where(item => item.TaskId is not null).Select(item => item.TaskId));
    }

    [Fact]
    public async Task ExecutorAuditsFailureWithoutStartingLaterTasks()
    {
        var trail = Trail(16);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var first = CreateTask("first");
        var second = CreateTask("second");
        var third = CreateTask("third");
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [first, second, third], PlannerStatus.Planned, null, null, context);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var gate = new ExecutionGate(new DefaultExecutionPolicy(), store, tools);
        var calls = 0;
        var executor = new PlanExecutor(new ExecutionPreflight(gate), gate, new FakeRouter(_ => ++calls == 2 ? ToolExecutionResult.Failure("SUPER_SECRET_EXEC_ERROR") : ToolExecutionResult.Success("ok")), Writer(trail), new TestTimeProvider());

        var result = await executor.ExecuteAsync(plan);
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(PlannerStatus.Failed, result.Status);
        Assert.Equal(new[]
        {
            AuditEventType.PlanExecutionStarted,
            AuditEventType.TaskExecutionStarted, AuditEventType.TaskExecutionCompleted,
            AuditEventType.TaskExecutionStarted, AuditEventType.TaskExecutionFailed,
            AuditEventType.PlanExecutionFailed
        }, events.Select(item => item.EventType));
        Assert.DoesNotContain(events, item => item.TaskId == third.Id);
        Assert.DoesNotContain("SUPER_SECRET_EXEC_ERROR", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task ExecutorRecordsTerminalCancellationAfterAnAlreadyCompletedTask()
    {
        var trail = Trail(16);
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var first = CreateTask("first");
        var second = CreateTask("second");
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [first, second], PlannerStatus.Planned, null, null, context);
        using var cancellation = new CancellationTokenSource();
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var gate = new ExecutionGate(new DefaultExecutionPolicy(), store, tools);
        var executor = new PlanExecutor(new ExecutionPreflight(gate), gate, new FakeRouter(_ =>
        {
            cancellation.Cancel();
            return ToolExecutionResult.Success("ok");
        }), Writer(trail), new TestTimeProvider());

        var result = await executor.ExecuteAsync(plan, cancellationToken: cancellation.Token);
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(PlannerStatus.Cancelled, result.Status);
        Assert.Equal(new[]
        {
            AuditEventType.PlanExecutionStarted,
            AuditEventType.TaskExecutionStarted,
            AuditEventType.TaskExecutionCompleted,
            AuditEventType.PlanExecutionCancelled
        }, events.Select(item => item.EventType));
        Assert.DoesNotContain(events, item => item.TaskId == second.Id);
    }

    [Theory]
    [InlineData(ToolExecutionStatus.Success, PlannerStatus.Completed)]
    [InlineData(ToolExecutionStatus.Failure, PlannerStatus.Failed)]
    public async Task ExecutorBehaviorIsUnchangedWhenAuditSinkFails(ToolExecutionStatus toolStatus, PlannerStatus expectedStatus)
    {
        var context = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Planner);
        var task = CreateTask("single");
        var plan = new Plan(Guid.NewGuid(), Guid.NewGuid(), [task], PlannerStatus.Planned, null, null, context);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var gate = new ExecutionGate(new DefaultExecutionPolicy(), store, tools);
        var router = new FakeRouter(new ToolExecutionResult(toolStatus, "SUPER_SECRET_EXEC_RESULT"));
        var executor = new PlanExecutor(new ExecutionPreflight(gate), gate, router, Writer(new ThrowingTrail()), new TestTimeProvider());

        var result = await executor.ExecuteAsync(plan);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(1, router.Calls);
    }

    private static InMemoryExecutionAuditTrail Trail(int maxEvents) =>
        new(Options.Create(new ExecutionAuditOptions { MaxEvents = maxEvents }));

    private static SafeExecutionAuditWriter Writer(IExecutionAuditTrail trail) => new(trail, NullLogger<SafeExecutionAuditWriter>.Instance);
    private static InMemoryExecutionApprovalStore Store() => new(Options.Create(new ExecutionPolicyOptions()), new TestTimeProvider());
    private static KaiAgent Agent(IExecutionAuditTrail trail)
    {
        var writer = Writer(trail);
        var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)));
        var store = Store();
        var preflight = new ExecutionPreflight(new ExecutionGate(new DefaultExecutionPolicy(), store, tools), writer, new TestTimeProvider());
        var builder = new PlanBuilder(writer, new TestTimeProvider());
        return new KaiAgent(new AuditConversation(), new DeterministicKaiIntentRouter(), new AuditRag(), new AuditChat(), new KernelPlanner(builder), new PlanExecutor(preflight, new ExecutionGate(new DefaultExecutionPolicy(), store, tools), new FakeRouter(ToolExecutionResult.Success("ok"))), null!, Options.Create(new KaiOptions()), writer, new TestTimeProvider());
    }
    private static ExecutionConfirmationService ConfirmationService(IExecutionAuditTrail trail)
    {
        var clock = new TestTimeProvider();
        var options = Options.Create(new ExecutionPolicyOptions());
        var approvals = new InMemoryExecutionApprovalStore(options, clock);
        var tools = new TestRegistry(new TestTool("write", new(false, true, false, ExecutionRiskLevel.High)));
        return new ExecutionConfirmationService(new DefaultExecutionPolicy(), approvals, new InMemoryExecutionPendingStore(options, clock), tools, clock, options, Writer(trail));
    }

    private static AuditEvent Event(AuditFlowId flowId, AuditEventType eventType) =>
        new(flowId, DateTimeOffset.UnixEpoch, eventType);

    private static PlanTask CreateTask(string name) => new(Guid.NewGuid(), name, "read", new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
}

internal sealed class AuditConversation : IConversationContextBuilder
{
    public Task<ConversationContextResult> BuildAsync(ConversationContextRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationContextResult(ConversationContextStatus.Success, new ConversationContextPack([], 0, 1, false)));
}

internal sealed class AuditRag : IRagPipeline
{
    public Task<RagResponse> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new RagResponse(RagStatus.Success, "response"));
}

internal sealed class AuditChat : IChatModel
{
    public Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ChatResponse("response", "test", 0, true, null));
}

internal sealed class ThrowingTrail : IExecutionAuditTrail
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("sink failure");
    public Task<IReadOnlyList<AuditEvent>> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
}
