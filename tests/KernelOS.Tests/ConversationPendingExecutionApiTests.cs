using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Tests;

[Collection("Side effect tool tests")]
public sealed class ConversationPendingExecutionApiTests
{
    [Fact]
    public async Task ConversationPendingExecutionsAreDiscoverablePagedIsolatedAndSafe()
    {
        using var factory = new TestApplicationFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var client = factory.CreateClient();
        var first = await ConversationEndpointTestsId.CreateAsync(client);
        var second = await ConversationEndpointTestsId.CreateAsync(client);

        var firstPending = await CreatePendingAsync(client, first, "SUPER_SECRET_ARGUMENT");
        await CreatePendingAsync(client, first, "SUPER_SECRET_ARGUMENT_2");
        var firstResponse = await client.GetAsync($"/conversations/{first}/pending-executions?limit=1&offset=0");
        var secondResponse = await client.GetAsync($"/conversations/{second}/pending-executions?limit=10&offset=0");
        var missingResponse = await client.GetAsync($"/conversations/{Guid.NewGuid()}/pending-executions");

        var body = await firstResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("Pending", json.RootElement[0].GetProperty("status").GetString());
        Assert.True(json.RootElement[0].GetProperty("confirmation").ValueKind == JsonValueKind.Object);
        Assert.DoesNotContain("SUPER_SECRET_ARGUMENT", body, StringComparison.Ordinal);
        Assert.DoesNotContain("approvalId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planId", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(0, (await secondResponse.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.NotEqual(Guid.Empty, firstPending);
    }

    [Fact]
    public async Task RestartedHostReturnsUnavailableForDurableCorrelationAndConversationDeleteDoesNotCancelPending()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
        Guid conversationId;
        Guid pendingId;
        using (var first = new TestApplicationFactory(true, directory, deleteDirectoryOnDispose: false).WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SideEffectTestTool>())))
        {
            var client = first.CreateClient();
            conversationId = await ConversationEndpointTestsId.CreateAsync(client);
            pendingId = await CreatePendingAsync(client, conversationId, "secret");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/execution/confirmations/{pendingId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/conversations/{conversationId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/execution/confirmations/{pendingId}")).StatusCode);
        }

        using var restarted = new TestApplicationFactory(true, directory).WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var restartedClient = restarted.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await restartedClient.GetAsync($"/conversations/{conversationId}/pending-executions")).StatusCode);

        var retainedConversation = await ConversationEndpointTestsId.CreateAsync(restartedClient);
        var retainedPending = await CreatePendingAsync(restartedClient, retainedConversation, "secret");
        using var retainedHost = new TestApplicationFactory(true, directory, deleteDirectoryOnDispose: false).WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SideEffectTestTool>()));
        using var retainedClient = retainedHost.CreateClient();
        var response = await retainedClient.GetAsync($"/conversations/{retainedConversation}/pending-executions");
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(retainedPending, responseJson.RootElement[0].GetProperty("pendingExecutionId").GetGuid());
        Assert.Equal("Unavailable", responseJson.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConfirmationAndExecutionResponsesDoNotExposeApprovalsPlansArgumentsOrToolResults()
    {
        using var factory = new TestApplicationFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IKernelTool, SecretResultTool>()));
        using var client = factory.CreateClient();
        using var pendingResponse = await client.PostAsync("/planner/execute", JsonContent.Create(new { goal = "EJECUTAR", tool = "secret-result-test", arguments = new { secret = "SUPER_SECRET_ARGUMENT" } }));
        using var pendingJson = JsonDocument.Parse(await pendingResponse.Content.ReadAsStringAsync());
        var pendingId = pendingJson.RootElement.GetProperty("pendingExecutionId").GetGuid();

        using var statusResponse = await client.GetAsync($"/execution/confirmations/{pendingId}");
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        using var approveResponse = await client.PostAsync($"/execution/confirmations/{pendingId}", JsonContent.Create(new { decision = "approve" }));
        var approveBody = await approveResponse.Content.ReadAsStringAsync();
        using var executionResponse = await client.PostAsync($"/execution/pending/{pendingId}/execute", null);
        var executionBody = await executionResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, executionResponse.StatusCode);
        Assert.Contains("Completed", executionBody, StringComparison.Ordinal);
        foreach (var body in new[] { statusBody, approveBody, executionBody })
        {
            Assert.DoesNotContain("SUPER_SECRET_ARGUMENT", body, StringComparison.Ordinal);
            Assert.DoesNotContain("SUPER_SECRET_TOOL_RESULT", body, StringComparison.Ordinal);
            Assert.DoesNotContain("approvalId", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"arguments\":", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"plan\":", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<Guid> CreatePendingAsync(HttpClient client, Guid conversationId, string secret)
    {
        using var response = await client.PostAsync($"/conversations/{conversationId}/messages", JsonContent.Create(new { message = "execute", preferredMode = "Planner", toolName = "side-effect-test", arguments = new { secret } }));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return json.RootElement.GetProperty("pendingExecutionId").GetGuid();
    }

    private sealed class SecretResultTool : IKernelTool
    {
        public string Name => "secret-result-test";
        public string Description => "Secret result test tool";
        public string Category => "test";
        public IReadOnlyCollection<ToolCapability> Capabilities => Array.Empty<ToolCapability>();
        public IReadOnlyCollection<ToolParameter> Parameters => Array.Empty<ToolParameter>();
        public ToolExecutionMetadata ExecutionMetadata => new(false, true, false, ExecutionRiskLevel.High);
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(ToolExecutionResult.Success("SUPER_SECRET_TOOL_RESULT", JsonSerializer.SerializeToElement(new { secret = "SUPER_SECRET_TOOL_RESULT" })));
    }
}
