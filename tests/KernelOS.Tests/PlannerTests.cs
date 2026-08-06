using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
namespace KernelOS.Tests;
public sealed class PlannerTests
{
    [Fact] public void GoalAndPlanCanBeCreated() { var goal = CreateGoal(); var plan = new Plan(Guid.NewGuid(), goal.Id, Array.Empty<PlanTask>(), PlannerStatus.Created, null, null); Assert.Equal(goal.Id, plan.GoalId); }
    [Fact] public async Task PlannerCompletesUsingRouter() { var planner = new KernelPlanner(new FakeRouter(ToolExecutionResult.Success("ok")), NullLogger<KernelPlanner>.Instance); var result = await planner.PlanAsync(CreateGoal()); Assert.Equal(PlannerStatus.Completed, result.Status); Assert.Equal(PlannerStatus.Completed, result.Plan!.Tasks.Single().Status); }
    [Fact] public async Task PlannerFailsForMissingTool() { var planner = new KernelPlanner(new FakeRouter(ToolExecutionResult.NotFound("missing")), NullLogger<KernelPlanner>.Instance); var result = await planner.PlanAsync(CreateGoal()); Assert.Equal(PlannerStatus.Failed, result.Status); }
    [Fact] public async Task PlannerCancels() { var planner = new KernelPlanner(new FakeRouter(ToolExecutionResult.Success("ok")), NullLogger<KernelPlanner>.Instance); using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.Equal(PlannerStatus.Cancelled, (await planner.PlanAsync(CreateGoal(), cancellation.Token)).Status); }
    [Fact] public async Task PlannerRejectsInvalidGoal() { var planner = new KernelPlanner(new FakeRouter(ToolExecutionResult.Success("ok")), NullLogger<KernelPlanner>.Instance); Assert.Equal("invalid_goal", (await planner.PlanAsync(new Goal(Guid.NewGuid(), " ", DateTimeOffset.UtcNow, 0))).Error!.Code); }
    private static Goal CreateGoal() => new(Guid.NewGuid(), "EJECUTAR Echo", DateTimeOffset.UtcNow, 0, new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement("echo"), ["arguments"] = JsonSerializer.SerializeToElement(new { text = "Hola" }) });
}
public sealed class PlannerEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] public async Task PostPlannerExecuteCompletesEcho() { using var response = await factory.CreateClient().PostAsync("/planner/execute", JsonContent.Create(new { goal = "EJECUTAR Echo", tool = "echo", arguments = new { text = "Hola" } })); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
}
internal sealed class FakeRouter(ToolExecutionResult result) : IToolRouter { public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result); }
