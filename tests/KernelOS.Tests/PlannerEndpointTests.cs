using System.Net;
using System.Net.Http.Json;
using KernelOS.Core.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Tests;

public sealed class PlannerEndpointTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task PostPlannerExecuteUsesExplicitExecutor()
    {
        using var response = await factory.CreateClient().PostAsync(
            "/planner/execute",
            JsonContent.Create(new { goal = "EJECUTAR Echo", tool = "echo", arguments = new { text = "Hola" } }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostPlannerExecuteReturnsConfirmationRequirementFromGate()
    {
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IExecutionGate>(new BlockingGate(ExecutionGateStatus.RequiresConfirmation))));
        using var response = await customFactory.CreateClient().PostAsync(
            "/planner/execute",
            JsonContent.Create(new { goal = "EJECUTAR Echo", tool = "echo", arguments = new { text = "Hola" } }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostPlannerExecuteReturnsForbiddenWhenGateDenies()
    {
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IExecutionGate>(new BlockingGate(ExecutionGateStatus.Denied))));
        using var response = await customFactory.CreateClient().PostAsync(
            "/planner/execute",
            JsonContent.Create(new { goal = "EJECUTAR Echo", tool = "echo", arguments = new { text = "Hola" } }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
