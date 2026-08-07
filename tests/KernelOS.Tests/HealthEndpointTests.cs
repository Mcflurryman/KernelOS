using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using KernelOS.Core;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KernelOS.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealthReturnsOk()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthResponseContainsKernelOS()
    {
        var content = await (await factory.CreateClient().GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.Contains("KernelOS", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHealthResponseContainsKai()
    {
        var content = await (await factory.CreateClient().GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.Contains("Kai", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHealthReturnsProductVersionFromAssemblyMetadata()
    {
        var status = await (await factory.CreateClient().GetAsync("/health"))
            .Content.ReadFromJsonAsync<SystemStatusResponse>();
        var informationalVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var expectedVersion = informationalVersion?.Split('+', 2)[0];

        Assert.NotNull(status);
        Assert.Equal("ok", status.Status);
        Assert.Equal("KernelOS", status.Application);
        Assert.Equal("Kai", status.Assistant);
        Assert.False(string.IsNullOrWhiteSpace(status.Version));
        Assert.NotEqual("0.1.0", status.Version);
        Assert.Equal(expectedVersion, status.Version);
        Assert.Matches(new Regex("^\\d+\\.\\d+\\.\\d+$"), status.Version);
    }
}

public sealed class KernelToolContractTests
{
    [Fact]
    public async Task IKernelToolCanBeImplementedByAFakeTool()
    {
        IKernelTool tool = new FakeKernelTool();

        await tool.ExecuteAsync(new ToolExecutionRequest("fake", new Dictionary<string, System.Text.Json.JsonElement>()));

        Assert.Equal("fake", tool.Name);
        Assert.Equal("A fake tool used for testing.", tool.Description);
    }

    private sealed class FakeKernelTool : IKernelTool
    {
        public string Name => "fake";

        public string Description => "A fake tool used for testing.";

        public string Category => "test";

        public IReadOnlyCollection<ToolCapability> Capabilities => Array.Empty<ToolCapability>();

        public IReadOnlyCollection<ToolParameter> Parameters => Array.Empty<ToolParameter>();

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ToolExecutionResult.Success("The fake tool completed."));
    }
}
