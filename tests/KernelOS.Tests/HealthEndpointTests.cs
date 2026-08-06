using System.Net;
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
}

public sealed class KernelToolContractTests
{
    [Fact]
    public async Task IKernelToolCanBeImplementedByAFakeTool()
    {
        IKernelTool tool = new FakeKernelTool();

        await tool.ExecuteAsync();

        Assert.Equal("fake", tool.Name);
        Assert.Equal("A fake tool used for testing.", tool.Description);
    }

    private sealed class FakeKernelTool : IKernelTool
    {
        public string Name => "fake";

        public string Description => "A fake tool used for testing.";

        public Task ExecuteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
