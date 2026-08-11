using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure;

namespace KernelOS.Tests;

public sealed class PlannerCoreTests
{
    [Fact]
    public void GoalAndPlanCanBeCreated()
    {
        var goal = PlannerTestData.CreateGoal();
        var plan = new Plan(Guid.NewGuid(), goal.Id, Array.Empty<PlanTask>(), PlannerStatus.Created, null, null);

        Assert.Equal(goal.Id, plan.GoalId);
    }

    [Fact]
    public async Task BuilderCreatesPlannedPlanWithIdentifiersAndPreservedArguments()
    {
        var result = await new PlanBuilder().BuildAsync(PlannerTestData.CreateGoal());
        var task = Assert.Single(result.Plan!.Tasks);

        Assert.Equal(PlannerStatus.Planned, result.Status);
        Assert.NotEqual(Guid.Empty, result.Plan.Id);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(PlannerStatus.Planned, task.Status);
        Assert.Equal("Hola", task.Arguments["text"].GetString());
    }

    [Fact]
    public async Task PlannerBuildsPlanWithoutExecutingRouter()
    {
        var router = new FakeRouter(ToolExecutionResult.Success("ok"));
        var planner = new KernelPlanner(new PlanBuilder());

        await planner.PlanAsync(PlannerTestData.CreateGoal());
        await planner.PlanAsync(PlannerTestData.CreateGoal());

        Assert.Equal(0, router.Calls);
    }

    [Fact]
    public async Task BuilderRejectsInvalidAndUnsupportedGoals()
    {
        var builder = new PlanBuilder();
        var invalid = await builder.BuildAsync(new Goal(Guid.Empty, " ", DateTimeOffset.UtcNow, 0));
        var unsupported = await builder.BuildAsync(new Goal(Guid.NewGuid(), "haz algo", DateTimeOffset.UtcNow, 0));

        Assert.Equal("invalid_goal", invalid.Error!.Code);
        Assert.Equal("unsupported_goal", unsupported.Error!.Code);
    }

    [Fact]
    public async Task BuilderCancelsBeforePlanning()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await new PlanBuilder().BuildAsync(PlannerTestData.CreateGoal(), cancellation.Token);

        Assert.Equal(PlannerStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task PlannerCancels()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await new KernelPlanner(new PlanBuilder()).PlanAsync(PlannerTestData.CreateGoal(), cancellation.Token);

        Assert.Equal(PlannerStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task PlannerRejectsInvalidGoal()
    {
        var result = await new KernelPlanner(new PlanBuilder()).PlanAsync(
            new Goal(Guid.NewGuid(), " ", DateTimeOffset.UtcNow, 0));

        Assert.Equal("invalid_goal", result.Error!.Code);
    }
}
