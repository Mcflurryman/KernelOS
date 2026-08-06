using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core;
using KernelOS.Tools;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace KernelOS.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void RegistryListsAndFindsRegisteredTools()
    {
        var registry = CreateRegistry();

        Assert.Equal(2, registry.Tools.Count);
        Assert.True(registry.Exists("ECHO"));
        Assert.IsType<EchoTool>(registry.GetByName("echo"));
    }

    [Fact]
    public void RegistryFindsToolsByCategoryAndCapability()
    {
        var registry = CreateRegistry();

        Assert.Equal(2, registry.FindByCategory("demonstration").Count);
        Assert.Single(registry.FindByCapability("echo"));
        Assert.Single(registry.FindByCapability("LOCAL-TIME"));
    }

    [Fact]
    public void RegistryRejectsDuplicateNames()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new KernelToolRegistry([new EchoTool(), new DuplicateEchoTool()]));

        Assert.Contains("echo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddKernelToolsRegistersTheToolSystem()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKernelTools();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<KernelToolRegistry>(provider.GetRequiredService<IToolRegistry>());
        Assert.IsType<KernelToolRouter>(provider.GetRequiredService<IToolRouter>());
        Assert.Equal(4, provider.GetServices<IKernelTool>().Count());
    }

    private static KernelToolRegistry CreateRegistry() => new([new EchoTool(), new TimeTool()]);
}

public sealed class ToolExecutionTests
{
    [Fact]
    public async Task EchoToolReturnsTheExactText()
    {
        var result = await new EchoTool().ExecuteAsync(CreateRequest("echo", ("text", "Hola Kai")));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal("Hola Kai", result.Data!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task EchoToolRejectsMissingText()
    {
        var result = await new EchoTool().ExecuteAsync(CreateRequest("echo"));

        Assert.Equal(ToolExecutionStatus.InvalidArguments, result.Status);
    }

    [Fact]
    public async Task TimeToolReturnsLocalTime()
    {
        var result = await new TimeTool().ExecuteAsync(CreateRequest("time"));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.True(result.Data!.Value.TryGetProperty("localTime", out _));
    }

    [Fact]
    public async Task RouterReturnsNotFoundForUnknownTool()
    {
        var router = CreateRouter(new KernelToolRegistry([new EchoTool()]));

        var result = await router.ExecuteAsync(CreateRequest("missing"));

        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RouterReturnsCancelledWhenCancellationIsRequested()
    {
        var router = CreateRouter(new KernelToolRegistry([new EchoTool()]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await router.ExecuteAsync(CreateRequest("echo", ("text", "Hola")), cancellation.Token);

        Assert.Equal(ToolExecutionStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task RouterConvertsUnexpectedExceptionsToFailure()
    {
        var router = CreateRouter(new KernelToolRegistry([new ThrowingTool()]));

        var result = await router.ExecuteAsync(CreateRequest("throwing"));

        Assert.Equal(ToolExecutionStatus.Failure, result.Status);
        Assert.DoesNotContain("unexpected", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static KernelToolRouter CreateRouter(IToolRegistry registry) =>
        new(registry, NullLogger<KernelToolRouter>.Instance);

    private static ToolExecutionRequest CreateRequest(string name, params (string Name, string Value)[] arguments)
    {
        var values = arguments.ToDictionary(
            item => item.Name,
            item => JsonSerializer.SerializeToElement(item.Value));

        return new ToolExecutionRequest(name, values);
    }
}

public sealed class ToolEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetToolsReturnsRegisteredTools()
    {
        using var response = await factory.CreateClient().GetAsync("/tools");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("echo", content, StringComparison.Ordinal);
        Assert.Contains("time", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetToolByNameReturnsToolAndUnknownNameReturnsNotFound()
    {
        using var known = await factory.CreateClient().GetAsync("/tools/echo");
        using var unknown = await factory.CreateClient().GetAsync("/tools/unknown");

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task PostEchoReturnsText()
    {
        using var response = await factory.CreateClient().PostAsync(
            "/tools/echo",
            JsonContent.Create(new { arguments = new { text = "Hola" } }));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hola", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostToolMapsInvalidArgumentsAndMissingTools()
    {
        using var invalid = await factory.CreateClient().PostAsync(
            "/tools/echo",
            JsonContent.Create(new { arguments = new { } }));
        using var missing = await factory.CreateClient().PostAsync(
            "/tools/missing",
            JsonContent.Create(new { arguments = new { } }));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}

internal sealed class DuplicateEchoTool : IKernelTool
{
    public string Name => "ECHO";

    public string Description => "Duplicate tool for testing.";

    public string Category => "test";

    public IReadOnlyCollection<ToolCapability> Capabilities => Array.Empty<ToolCapability>();

    public IReadOnlyCollection<ToolParameter> Parameters => Array.Empty<ToolParameter>();

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ToolExecutionResult.Success("Completed."));
}

internal sealed class ThrowingTool : IKernelTool
{
    public string Name => "throwing";

    public string Description => "Throws during execution for testing.";

    public string Category => "test";

    public IReadOnlyCollection<ToolCapability> Capabilities => Array.Empty<ToolCapability>();

    public IReadOnlyCollection<ToolParameter> Parameters => Array.Empty<ToolParameter>();

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected failure.");
}
