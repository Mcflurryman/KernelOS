using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure;
using KernelOS.Infrastructure.Execution;
using KernelOS.Tools;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class ExecutionPolicyTests
{
    [Fact] public void PolicyAllowsKnownReadOnlyTool() => Assert.Equal(ExecutionPolicyDecisionType.Allow, Policy().Evaluate(Request(new(true, false, false, ExecutionRiskLevel.Low))).Type);
    [Fact] public void PolicyRequiresConfirmationForSideEffects() { var decision = Policy().Evaluate(Request(new(false, true, false, ExecutionRiskLevel.High))); Assert.Equal(ExecutionPolicyDecisionType.RequireConfirmation, decision.Type); Assert.Equal(ExecutionPolicyReason.SideEffectRequiresConfirmation, decision.Reason); }
    [Fact] public void PolicyDeniesExplicitlyDeniedTool() => Assert.Equal(ExecutionPolicyDecisionType.Deny, Policy().Evaluate(Request(new(false, true, true, ExecutionRiskLevel.Critical))).Type);
    [Fact] public void PolicyFailsClosedForUnknownTool() => Assert.Equal(ExecutionPolicyDecisionType.RequireConfirmation, Policy().Evaluate(Request(null)).Type);
    [Fact] public void PolicyFailsClosedForInsufficientMetadata() { var decision = Policy().Evaluate(Request(new(false, false, false, ExecutionRiskLevel.Medium))); Assert.Equal(ExecutionPolicyDecisionType.RequireConfirmation, decision.Type); Assert.Equal(ExecutionPolicyReason.UnknownToolRequiresConfirmation, decision.Reason); }
    [Fact] public void PolicyRejectsInvalidRequest() => Assert.Equal(ExecutionPolicyReason.InvalidRequest, Policy().Evaluate(new(Guid.Empty, Guid.Empty, "", null)).Reason);

    [Fact] public async Task ApprovalIsScopedAndOneShot() { var store = Store(); var task = CreateTask("write"); var approval = await store.CreateAsync(Guid.NewGuid(), task.Id, ExecutionTaskFingerprint.Create(task)); Assert.False(await store.TryConsumeAsync(approval.Id, Guid.NewGuid(), task.Id, approval.TaskFingerprint)); Assert.True(await store.TryConsumeAsync(approval.Id, approval.PlanId, task.Id, approval.TaskFingerprint)); Assert.False(await store.TryConsumeAsync(approval.Id, approval.PlanId, task.Id, approval.TaskFingerprint)); }
    [Fact] public async Task ApprovalExpiresAtItsExactExpiryBoundary() { var clock = new TestTimeProvider(); var store = Store(clock, 1); var task = CreateTask("write"); var approval = await store.CreateAsync(Guid.NewGuid(), task.Id, ExecutionTaskFingerprint.Create(task)); clock.Advance(TimeSpan.FromMinutes(1)); Assert.False(await store.TryConsumeAsync(approval.Id, approval.PlanId, task.Id, approval.TaskFingerprint)); }
    [Fact] public async Task ApprovalCannotBeConsumedTwiceConcurrently() { var store = Store(); var task = CreateTask("write"); var approval = await store.CreateAsync(Guid.NewGuid(), task.Id, ExecutionTaskFingerprint.Create(task)); var results = await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0, 64).Select(_ => store.TryConsumeAsync(approval.Id, approval.PlanId, task.Id, approval.TaskFingerprint))); Assert.Equal(1, results.Count(result => result)); }
    [Fact] public void FingerprintIsDeterministicAndProtectsArguments() { var first = CreateTask("write", new Dictionary<string, JsonElement> { ["b"] = JsonSerializer.SerializeToElement(new { nested = new { b = 2, a = 1 } }), ["a"] = JsonSerializer.SerializeToElement(1) }); var reordered = CreateTask("write", new Dictionary<string, JsonElement> { ["a"] = JsonSerializer.SerializeToElement(1), ["b"] = JsonSerializer.SerializeToElement(new { nested = new { a = 1, b = 2 } }) }); var changed = first with { Arguments = new Dictionary<string, JsonElement> { ["a"] = JsonSerializer.SerializeToElement(3) } }; using var arrayDocument = JsonDocument.Parse("[1,2]"); var changedArray = first with { Arguments = new Dictionary<string, JsonElement> { ["items"] = arrayDocument.RootElement.Clone() } }; Assert.Equal(ExecutionTaskFingerprint.Create(first), ExecutionTaskFingerprint.Create(reordered)); Assert.NotEqual(ExecutionTaskFingerprint.Create(first), ExecutionTaskFingerprint.Create(changed)); Assert.NotEqual(ExecutionTaskFingerprint.Create(first), ExecutionTaskFingerprint.Create(first with { ToolName = "other" })); Assert.NotEqual(ExecutionTaskFingerprint.Create(first), ExecutionTaskFingerprint.Create(changedArray)); }

    [Fact] public async Task GateRequiresApprovalForSideEffectAndAllowsValidScopedApproval() { var planId = Guid.NewGuid(); var task = CreateTask("write"); var store = Store(); var gate = Gate(store, "write", new(false, true, false, ExecutionRiskLevel.High)); Assert.Equal(ExecutionGateStatus.RequiresConfirmation, (await gate.EvaluateAsync(planId, task, null)).Status); var approval = await store.CreateAsync(planId, task.Id, ExecutionTaskFingerprint.Create(task)); Assert.Equal(ExecutionGateStatus.Authorized, (await gate.EvaluateAsync(planId, task, approval.Id)).Status); }
    [Fact] public async Task GateDoesNotLetApprovalBypassDeny() { var planId = Guid.NewGuid(); var task = CreateTask("blocked"); var store = Store(); var approval = await store.CreateAsync(planId, task.Id, ExecutionTaskFingerprint.Create(task)); Assert.Equal(ExecutionGateStatus.Denied, (await Gate(store, "blocked", new(false, true, true, ExecutionRiskLevel.Critical)).EvaluateAsync(planId, task, approval.Id)).Status); }
    [Fact] public async Task GateDoesNotLetApprovalTrustUnknownTool() { var planId = Guid.NewGuid(); var task = CreateTask("unknown"); var store = Store(); var approval = await store.CreateAsync(planId, task.Id, ExecutionTaskFingerprint.Create(task)); var gate = new ExecutionGate(Policy(), store, new TestRegistry()); Assert.Equal(ExecutionGateStatus.RequiresConfirmation, (await gate.EvaluateAsync(planId, task, approval.Id)).Status); Assert.True(await store.TryConsumeAsync(approval.Id, planId, task.Id, approval.TaskFingerprint)); }

    [Fact] public async Task DirectGatewayDoesNotExecuteSideEffects() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var gateway = new ReadOnlyToolExecutionGateway(Policy(), new TestRegistry(new TestTool("write", new(false, true, false, ExecutionRiskLevel.High))), router); var result = await gateway.ExecuteAsync(new ToolExecutionRequest("write", new Dictionary<string, JsonElement>())); Assert.Equal(ToolExecutionStatus.Unauthorized, result.Status); Assert.Equal(0, router.Calls); }
    [Fact] public async Task DirectGatewayExecutesKnownReadOnlyTools() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var gateway = new ReadOnlyToolExecutionGateway(Policy(), new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low))), router); var result = await gateway.ExecuteAsync(new ToolExecutionRequest("read", new Dictionary<string, JsonElement>())); Assert.Equal(ToolExecutionStatus.Success, result.Status); Assert.Equal(1, router.Calls); }
    [Fact] public async Task ExecutorBlocksSideEffectWithoutApproval() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var result = await Executor(Store(), "write", new(false, true, false, ExecutionRiskLevel.High), router).ExecuteAsync(Plan(CreateTask("write"))); Assert.Equal(PlannerStatus.RequiresConfirmation, result.Status); Assert.Equal(0, router.Calls); }
    [Fact] public async Task ExecutorExecutesApprovedSideEffectAndRejectsChangedTask() { var task = CreateTask("write"); var plan = Plan(task); var store = Store(); var approval = await store.CreateAsync(plan.Id, task.Id, ExecutionTaskFingerprint.Create(task)); var router = new FakeRouter(ToolExecutionResult.Success("ok")); var executor = Executor(store, "write", new(false, true, false, ExecutionRiskLevel.High), router); Assert.Equal(PlannerStatus.Completed, (await executor.ExecuteAsync(plan, new Dictionary<Guid, Guid> { [task.Id] = approval.Id })).Status); var changed = task with { ToolName = "other" }; var secondPlan = Plan(changed); var approval2 = await store.CreateAsync(secondPlan.Id, changed.Id, ExecutionTaskFingerprint.Create(task)); Assert.Equal(PlannerStatus.RequiresConfirmation, (await Executor(store, "other", new(false, true, false, ExecutionRiskLevel.High), router).ExecuteAsync(secondPlan, new Dictionary<Guid, Guid> { [changed.Id] = approval2.Id })).Status); Assert.Equal(1, router.Calls); }
    [Fact] public async Task ExecutorStopsMultiTaskAtConfirmationAndPreservesPendingTasks() { var first = CreateTask("read"); var second = CreateTask("write"); var third = CreateTask("read"); var router = new FakeRouter(ToolExecutionResult.Success("ok")); var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)), new TestTool("write", new(false, true, false, ExecutionRiskLevel.High))); var store = Store(); var executor = new PlanExecutor(new ExecutionGate(Policy(), store, tools), router); var result = await executor.ExecuteAsync(Plan(first, second, third)); Assert.Equal(PlannerStatus.RequiresConfirmation, result.Status); Assert.Equal(1, router.Calls); Assert.Equal([PlannerStatus.Completed, PlannerStatus.RequiresConfirmation, PlannerStatus.Planned], result.Plan!.Tasks.Select(task => task.Status)); }
    [Fact] public async Task ExecutorRejectsPartialPlanInsteadOfRepeatingCompletedTasks() { var first = CreateTask("read"); var second = CreateTask("write"); var router = new FakeRouter(ToolExecutionResult.Success("ok")); var tools = new TestRegistry(new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low)), new TestTool("write", new(false, true, false, ExecutionRiskLevel.High))); var executor = new PlanExecutor(new ExecutionGate(Policy(), Store(), tools), router); var partial = await executor.ExecuteAsync(Plan(first, second)); var retry = await executor.ExecuteAsync(partial.Plan!); Assert.Equal(PlannerStatus.RequiresConfirmation, partial.Status); Assert.Equal(PlannerStatus.Failed, retry.Status); Assert.Equal("invalid_plan", retry.Error!.Code); Assert.Equal(1, router.Calls); }

    private static DefaultExecutionPolicy Policy() => new();
    private static ExecutionPolicyRequest Request(ToolExecutionMetadata? metadata) => new(Guid.NewGuid(), Guid.NewGuid(), "tool", metadata);
    private static InMemoryExecutionApprovalStore Store(TestTimeProvider? clock = null, int ttl = 5) => new(Options.Create(new ExecutionPolicyOptions { ApprovalTtlMinutes = ttl }), clock ?? new TestTimeProvider());
    private static ExecutionGate Gate(IExecutionApprovalStore store, string name, ToolExecutionMetadata metadata) => new(Policy(), store, new TestRegistry(new TestTool(name, metadata)));
    private static PlanExecutor Executor(IExecutionApprovalStore store, string name, ToolExecutionMetadata metadata, IToolRouter router) => new(Gate(store, name, metadata), router);
    private static Plan Plan(params PlanTask[] tasks) => new(Guid.NewGuid(), Guid.NewGuid(), tasks, PlannerStatus.Planned, null, null);
    private static PlanTask CreateTask(string name, IReadOnlyDictionary<string, JsonElement>? arguments = null) => new(Guid.NewGuid(), "task", name, arguments ?? new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
}

internal sealed class TestRegistry(params IKernelTool[] tools) : IToolRegistry
{
    public IReadOnlyCollection<IKernelTool> Tools { get; } = tools;
    public IKernelTool? GetByName(string name) => Tools.SingleOrDefault(tool => string.Equals(tool.Name, name, StringComparison.OrdinalIgnoreCase));
    public bool Exists(string name) => GetByName(name) is not null;
    public IReadOnlyCollection<IKernelTool> FindByCategory(string category) => [];
    public IReadOnlyCollection<IKernelTool> FindByCapability(string capability) => [];
}

internal sealed class TestTool(string name, ToolExecutionMetadata metadata) : IKernelTool
{
    public string Name => name; public string Description => name; public string Category => "test"; public IReadOnlyCollection<ToolCapability> Capabilities => []; public IReadOnlyCollection<ToolParameter> Parameters => []; public ToolExecutionMetadata ExecutionMetadata => metadata;
    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(ToolExecutionResult.Success("ok"));
}

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset now = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now = now.Add(duration);
}
