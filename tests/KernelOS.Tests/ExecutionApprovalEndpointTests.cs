using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Tests;

[Collection("Side effect tool tests")]
public sealed class ExecutionApprovalEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task SideEffectRequiresApprovalAndExecutesOnlyFromApprovedPendingSnapshot()
    {
        SideEffectTestTool.Calls = 0;
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var client = customFactory.CreateClient();
        using var pendingResponse = await client.PostAsync(
            "/planner/execute",
            JsonContent.Create(new { goal = "EJECUTAR", tool = "side-effect-test", arguments = new { value = "client input" } }));

        Assert.Equal(HttpStatusCode.Conflict, pendingResponse.StatusCode);

        using var pendingJson = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var id = pendingJson.RootElement.GetProperty("pendingExecutionId").GetGuid();
        using var approvalResponse = await client.PostAsync(
            $"/execution/confirmations/{id}",
            JsonContent.Create(new { decision = "approve" }));

        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);

        using var executionResponse = await client.PostAsync($"/execution/pending/{id}/execute", null);

        Assert.Equal(HttpStatusCode.OK, executionResponse.StatusCode);
        Assert.Equal(1, SideEffectTestTool.Calls);

        using var repeatedExecution = await client.PostAsync($"/execution/pending/{id}/execute", null);

        Assert.Equal(HttpStatusCode.Conflict, repeatedExecution.StatusCode);
    }
}
