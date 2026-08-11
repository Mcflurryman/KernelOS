using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure;

namespace KernelOS.Tests;

public sealed class PlanExecutorTests
{
    [Fact]
    public async Task ExecutorCompletesUsingRouterWithCorrectArguments()
    {
        var router = new FakeRouter(ToolExecutionResult.Success("ok"));
        var plan = (await new PlanBuilder().BuildAsync(PlannerTestData.CreateGoal())).Plan!;

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(plan);

        Assert.Equal(PlannerStatus.Completed, result.Status);
        Assert.Equal(1, router.Calls);
        Assert.Equal("echo", router.Requests.Single().ToolName);
        Assert.Equal("Hola", router.Requests.Single().Arguments["text"].GetString());
    }

    [Fact]
    public async Task ExecutorFailsFastWhenFirstTaskFailsAndPreservesPendingTasks()
    {
        var router = new FakeRouter(ToolExecutionResult.Failure());

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(PlannerTestData.CreateMultiTaskPlan());

        Assert.Equal(PlannerStatus.Failed, result.Status);
        Assert.Equal("tool_failed", result.Error!.Code);
        Assert.Equal(1, router.Calls);
        Assert.Equal(2, result.Plan!.Tasks.Count);
        Assert.Equal(PlannerStatus.Failed, result.Plan.Tasks.ElementAt(0).Status);
        Assert.Equal(PlannerStatus.Planned, result.Plan.Tasks.ElementAt(1).Status);
    }

    [Fact]
    public async Task ExecutorRunsMultipleTasksSequentially()
    {
        var router = new FakeRouter(ToolExecutionResult.Success("ok"));

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(PlannerTestData.CreateMultiTaskPlan());

        Assert.Equal(PlannerStatus.Completed, result.Status);
        Assert.Equal(["first", "second"], router.Requests.Select(request => request.ToolName));
        Assert.All(result.Plan!.Tasks, task => Assert.Equal(PlannerStatus.Completed, task.Status));
    }

    [Fact]
    public async Task ExecutorRejectsInvalidPlanWithoutCallingRouter()
    {
        var router = new FakeRouter(ToolExecutionResult.Success("ok"));
        var invalid = PlannerTestData.CreateMultiTaskPlan() with { Tasks = [PlannerTestData.CreateTask(Guid.NewGuid(), "")] };

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(invalid);

        Assert.Equal("invalid_plan", result.Error!.Code);
        Assert.Equal(0, router.Calls);
    }

    [Fact]
    public async Task ExecutorReturnsCancelledWhenRouterCancelsAndPreservesPendingTasks()
    {
        var router = new FakeRouter(ToolExecutionResult.Cancelled());

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(PlannerTestData.CreateMultiTaskPlan());

        Assert.Equal(PlannerStatus.Cancelled, result.Status);
        Assert.Equal(1, router.Calls);
        Assert.Equal(2, result.Plan!.Tasks.Count);
        Assert.Equal(PlannerStatus.Cancelled, result.Plan.Tasks.ElementAt(0).Status);
        Assert.Equal(PlannerStatus.Planned, result.Plan.Tasks.ElementAt(1).Status);
    }

    [Fact]
    public async Task ExecutorStopsRemainingTasksWhenCancelledBetweenTasksAndPreservesThem()
    {
        using var cancellation = new CancellationTokenSource();
        var router = new FakeRouter(_ =>
        {
            cancellation.Cancel();
            return ToolExecutionResult.Success("ok");
        });

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(
            PlannerTestData.CreateMultiTaskPlan(),
            cancellationToken: cancellation.Token);

        Assert.Equal(PlannerStatus.Cancelled, result.Status);
        Assert.Equal(1, router.Calls);
        Assert.Equal(2, result.Plan!.Tasks.Count);
        Assert.Equal(PlannerStatus.Completed, result.Plan.Tasks.ElementAt(0).Status);
        Assert.Equal(PlannerStatus.Planned, result.Plan.Tasks.ElementAt(1).Status);
    }

    [Fact]
    public async Task ExecutorConvertsExceptionsToFailure()
    {
        var router = new FakeRouter(_ => throw new InvalidOperationException());
        var plan = (await new PlanBuilder().BuildAsync(PlannerTestData.CreateGoal())).Plan!;

        var result = await new PlanExecutor(new AllowingGate(), router).ExecuteAsync(plan);

        Assert.Equal(PlannerStatus.Failed, result.Status);
        Assert.Equal("execution_failed", result.Error!.Code);
    }
}
