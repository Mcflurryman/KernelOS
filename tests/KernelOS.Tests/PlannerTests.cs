using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
namespace KernelOS.Tests;
public sealed class PlannerTests
{
    [Fact] public void GoalAndPlanCanBeCreated() { var goal = CreateGoal(); var plan = new Plan(Guid.NewGuid(), goal.Id, Array.Empty<PlanTask>(), PlannerStatus.Created, null, null); Assert.Equal(goal.Id, plan.GoalId); }
    [Fact] public async Task BuilderCreatesPlannedPlanWithIdentifiersAndPreservedArguments() { var result = await new PlanBuilder().BuildAsync(CreateGoal()); var task = Assert.Single(result.Plan!.Tasks); Assert.Equal(PlannerStatus.Planned, result.Status); Assert.NotEqual(Guid.Empty, result.Plan.Id); Assert.NotEqual(Guid.Empty, task.Id); Assert.Equal(PlannerStatus.Planned, task.Status); Assert.Equal("Hola", task.Arguments["text"].GetString()); }
    [Fact] public async Task PlannerBuildsPlanWithoutExecutingRouter() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var planner = new KernelPlanner(new PlanBuilder()); await planner.PlanAsync(CreateGoal()); await planner.PlanAsync(CreateGoal()); Assert.Equal(0, router.Calls); }
    [Fact] public async Task BuilderRejectsInvalidAndUnsupportedGoals() { var builder = new PlanBuilder(); var invalid = await builder.BuildAsync(new Goal(Guid.Empty, " ", DateTimeOffset.UtcNow, 0)); var unsupported = await builder.BuildAsync(new Goal(Guid.NewGuid(), "haz algo", DateTimeOffset.UtcNow, 0)); Assert.Equal("invalid_goal", invalid.Error!.Code); Assert.Equal("unsupported_goal", unsupported.Error!.Code); }
    [Fact] public async Task BuilderCancelsBeforePlanning() { using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.Equal(PlannerStatus.Cancelled, (await new PlanBuilder().BuildAsync(CreateGoal(), cancellation.Token)).Status); }
    [Fact] public async Task ExecutorCompletesUsingRouterWithCorrectArguments() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var result = await new PlanExecutor(router).ExecuteAsync((await new PlanBuilder().BuildAsync(CreateGoal())).Plan!); Assert.Equal(PlannerStatus.Completed, result.Status); Assert.Equal(1, router.Calls); Assert.Equal("echo", router.Requests.Single().ToolName); Assert.Equal("Hola", router.Requests.Single().Arguments["text"].GetString()); }
    [Fact] public async Task ExecutorFailsFastWhenFirstTaskFails() { var router = new FakeRouter(ToolExecutionResult.Failure()); var result = await new PlanExecutor(router).ExecuteAsync(CreateMultiTaskPlan()); Assert.Equal(PlannerStatus.Failed, result.Status); Assert.Equal("tool_failed", result.Error!.Code); Assert.Equal(1, router.Calls); }
    [Fact] public async Task ExecutorRunsMultipleTasksSequentially() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var result = await new PlanExecutor(router).ExecuteAsync(CreateMultiTaskPlan()); Assert.Equal(PlannerStatus.Completed, result.Status); Assert.Equal(["first", "second"], router.Requests.Select(request => request.ToolName)); Assert.All(result.Plan!.Tasks, task => Assert.Equal(PlannerStatus.Completed, task.Status)); }
    [Fact] public async Task ExecutorRejectsInvalidPlanWithoutCallingRouter() { var router = new FakeRouter(ToolExecutionResult.Success("ok")); var invalid = CreateMultiTaskPlan() with { Tasks = [CreateTask(Guid.NewGuid(), "")] }; var result = await new PlanExecutor(router).ExecuteAsync(invalid); Assert.Equal("invalid_plan", result.Error!.Code); Assert.Equal(0, router.Calls); }
    [Fact] public async Task ExecutorReturnsCancelledWhenRouterCancels() { var router = new FakeRouter(ToolExecutionResult.Cancelled()); var result = await new PlanExecutor(router).ExecuteAsync(CreateMultiTaskPlan()); Assert.Equal(PlannerStatus.Cancelled, result.Status); Assert.Equal(1, router.Calls); }
    [Fact] public async Task ExecutorStopsRemainingTasksWhenCancelledBetweenTasks() { using var cancellation = new CancellationTokenSource(); var router = new FakeRouter(_ => { cancellation.Cancel(); return ToolExecutionResult.Success("ok"); }); var result = await new PlanExecutor(router).ExecuteAsync(CreateMultiTaskPlan(), cancellation.Token); Assert.Equal(PlannerStatus.Cancelled, result.Status); Assert.Equal(1, router.Calls); }
    [Fact] public async Task ExecutorConvertsExceptionsToFailure() { var router = new FakeRouter(_ => throw new InvalidOperationException()); var result = await new PlanExecutor(router).ExecuteAsync((await new PlanBuilder().BuildAsync(CreateGoal())).Plan!); Assert.Equal(PlannerStatus.Failed, result.Status); Assert.Equal("execution_failed", result.Error!.Code); }
    [Fact] public async Task PlannerCancels() { using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.Equal(PlannerStatus.Cancelled, (await new KernelPlanner(new PlanBuilder()).PlanAsync(CreateGoal(), cancellation.Token)).Status); }
    [Fact] public async Task PlannerRejectsInvalidGoal() { Assert.Equal("invalid_goal", (await new KernelPlanner(new PlanBuilder()).PlanAsync(new Goal(Guid.NewGuid(), " ", DateTimeOffset.UtcNow, 0))).Error!.Code); }
    private static Goal CreateGoal() => new(Guid.NewGuid(), "EJECUTAR Echo", DateTimeOffset.UtcNow, 0, new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement("echo"), ["arguments"] = JsonSerializer.SerializeToElement(new { text = "Hola" }) });
    private static Plan CreateMultiTaskPlan() => new(Guid.NewGuid(), Guid.NewGuid(), [CreateTask(Guid.NewGuid(), "first"), CreateTask(Guid.NewGuid(), "second")], PlannerStatus.Planned, null, null);
    private static PlanTask CreateTask(Guid id, string toolName) => new(id, "Execute requested tool", toolName, new Dictionary<string, JsonElement>(), PlannerStatus.Planned, 0);
}
public sealed class PlannerEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] public async Task PostPlannerExecuteUsesExplicitExecutor() { using var response = await factory.CreateClient().PostAsync("/planner/execute", JsonContent.Create(new { goal = "EJECUTAR Echo", tool = "echo", arguments = new { text = "Hola" } })); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
}
internal sealed class FakeRouter : IToolRouter
{
    private readonly Func<ToolExecutionRequest, ToolExecutionResult> execute;
    public FakeRouter(ToolExecutionResult result) : this(_ => result) { }
    public FakeRouter(Func<ToolExecutionRequest, ToolExecutionResult> execute) => this.execute = execute;
    public int Calls { get; private set; }
    public List<ToolExecutionRequest> Requests { get; } = [];
    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) { Calls++; Requests.Add(request); return Task.FromResult(execute(request)); }
}
