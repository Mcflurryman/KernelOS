using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Tests;

[Collection("Side effect tool tests")]
public sealed class KaiPlannerEndpointTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task PostKaiExecutesExplicitReadOnlyPlannerAction()
    {
        using var response = await factory.CreateClient().PostAsync(
            "/kai",
            JsonContent.Create(new
            {
                message = "execute echo",
                preferredMode = "Planner",
                toolName = "echo",
                arguments = new { text = "Hola" }
            }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostKaiRejectsInvalidRequest()
    {
        using var response = await factory.CreateClient().PostAsync("/kai", JsonContent.Create(new { message = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostKaiReturnsPendingConfirmationWithoutExecutingSideEffect()
    {
        SideEffectTestTool.Calls = 0;
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var response = await customFactory.CreateClient().PostAsync(
            "/kai",
            JsonContent.Create(new
            {
                message = "execute",
                preferredMode = "Planner",
                toolName = "side-effect-test",
                arguments = new { }
            }));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RequiresConfirmation", json.GetProperty("status").GetString());
        Assert.Equal("Planner", json.GetProperty("modeUsed").GetString());
        Assert.NotEqual(Guid.Empty, json.GetProperty("pendingExecutionId").GetGuid());
        Assert.True(json.GetProperty("confirmation").ValueKind == JsonValueKind.Object);
        Assert.Equal(0, SideEffectTestTool.Calls);
    }

    [Fact]
    public async Task PostKaiRejectsUnknownToolWithoutPendingConfirmation()
    {
        using var response = await factory.CreateClient().PostAsync(
            "/kai",
            JsonContent.Create(new
            {
                message = "execute",
                preferredMode = "Planner",
                toolName = "unknown-tool",
                arguments = new { }
            }));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PlanningFailed", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("pendingExecutionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("confirmation").ValueKind);
    }

    [Fact]
    public async Task PostKaiReturnsDeniedWithoutExecutingTool()
    {
        DeniedTestTool.Calls = 0;
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IKernelTool, DeniedTestTool>()));
        using var response = await customFactory.CreateClient().PostAsync(
            "/kai",
            JsonContent.Create(new
            {
                message = "execute",
                preferredMode = "Planner",
                toolName = "denied-test",
                arguments = new { }
            }));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Denied", json.GetProperty("status").GetString());
        Assert.Equal("Planner", json.GetProperty("modeUsed").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("pendingExecutionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("confirmation").ValueKind);
        Assert.Equal(0, DeniedTestTool.Calls);
    }

    [Fact]
    public async Task PostKaiConcurrentSideEffectsCreateIndependentPendingExecutions()
    {
        SideEffectTestTool.Calls = 0;
        using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var client = customFactory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => client.PostAsync(
            "/kai",
            JsonContent.Create(new
            {
                message = $"execute {index}",
                preferredMode = "Planner",
                toolName = "side-effect-test",
                arguments = new { index }
            }))));
        var bodies = await Task.WhenAll(responses.Select(async response =>
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using (response)
            {
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
            }
        }));

        Assert.NotEqual(bodies[0].GetProperty("planId").GetGuid(), bodies[1].GetProperty("planId").GetGuid());
        Assert.NotEqual(bodies[0].GetProperty("pendingExecutionId").GetGuid(), bodies[1].GetProperty("pendingExecutionId").GetGuid());
        Assert.Equal(0, SideEffectTestTool.Calls);
    }
}

internal sealed class DeniedTestTool : IKernelTool
{
    public static int Calls { get; set; }

    public string Name => "denied-test";
    public string Description => "Denied test tool.";
    public string Category => "test";
    public IReadOnlyCollection<ToolCapability> Capabilities => [];
    public IReadOnlyCollection<ToolParameter> Parameters => [];
    public ToolExecutionMetadata ExecutionMetadata => new(false, true, true, ExecutionRiskLevel.Critical);

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(ToolExecutionResult.Success("unexpected"));
    }
}
