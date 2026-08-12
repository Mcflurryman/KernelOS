using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Audit;
using KernelOS.Core.Execution;
using KernelOS.Infrastructure.Execution;
using KernelOS.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class ReadOnlyExecutionAuditTests
{
    [Fact]
    public async Task AllowedSuccessWritesStartedAndCompletedWithoutPayloads()
    {
        var trail = Trail();
        var gateway = Gateway(trail, new FakeRouter(ToolExecutionResult.Success("SUPER_SECRET_DIRECT_RESULT")));
        var request = Request("read", "SUPER_SECRET_DIRECT_ARGUMENT");

        var result = await gateway.ExecuteAsync(request);
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(new[] { AuditEventType.DirectReadOnlyExecutionStarted, AuditEventType.DirectReadOnlyExecutionCompleted }, events.Select(item => item.EventType));
        Assert.Single(events.Select(item => item.FlowId).Distinct());
        Assert.All(events, item => Assert.Equal(ExecutionOrigin.DirectReadOnly, item.Origin));
        Assert.True(events[1].Duration >= TimeSpan.Zero);
        Assert.DoesNotContain("SUPER_SECRET_DIRECT", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task AllowedFailureWritesFailedWithoutCompletedOrPayloads()
    {
        var trail = Trail();
        var gateway = Gateway(trail, new FakeRouter(ToolExecutionResult.Failure("SUPER_SECRET_DIRECT_ERROR")));

        var result = await gateway.ExecuteAsync(Request("read", "SUPER_SECRET_DIRECT_ARGUMENT"));
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(ToolExecutionStatus.Failure, result.Status);
        Assert.Equal(new[] { AuditEventType.DirectReadOnlyExecutionStarted, AuditEventType.DirectReadOnlyExecutionFailed }, events.Select(item => item.EventType));
        Assert.Equal(ToolExecutionStatus.Failure.ToString(), events[1].Status);
        Assert.Null(events.Single(item => item.EventType == AuditEventType.DirectReadOnlyExecutionFailed).ErrorCode);
        Assert.DoesNotContain("SUPER_SECRET_DIRECT", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task CancelledDirectExecutionPreservesResultWithoutInventingATerminalEvent()
    {
        var trail = Trail();
        var gateway = Gateway(trail, new FakeRouter(ToolExecutionResult.Cancelled()));

        var result = await gateway.ExecuteAsync(Request("read"));
        var events = await trail.GetSnapshotAsync();

        Assert.Equal(ToolExecutionStatus.Cancelled, result.Status);
        Assert.Equal([AuditEventType.DirectReadOnlyExecutionStarted], events.Select(item => item.EventType));
    }

    [Theory]
    [InlineData("write", false, true, false)]
    [InlineData("blocked", false, true, true)]
    [InlineData("unknown-metadata", false, false, false)]
    public async Task PolicyBlocksDoNotExecuteOrEmitExecutionEvents(string name, bool readOnly, bool sideEffects, bool denied)
    {
        var trail = Trail();
        var router = new FakeRouter(ToolExecutionResult.Success("unexpected"));
        var gateway = Gateway(trail, router, new TestTool(name, new(readOnly, sideEffects, denied, ExecutionRiskLevel.High)));

        var result = await gateway.ExecuteAsync(Request(name));

        Assert.Equal(ToolExecutionStatus.Unauthorized, result.Status);
        Assert.Equal(0, router.Calls);
        Assert.Empty(await trail.GetSnapshotAsync());
    }

    [Fact]
    public async Task UnknownToolFailsClosedWithoutExecutionAudit()
    {
        var trail = Trail();
        var router = new FakeRouter(ToolExecutionResult.Success("unexpected"));
        var gateway = Gateway(trail, router);

        var result = await gateway.ExecuteAsync(Request("SUPER_SECRET_DIRECT_TOOL_NAME"));

        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);
        Assert.Equal(0, router.Calls);
        Assert.Empty(await trail.GetSnapshotAsync());
    }

    [Theory]
    [InlineData(ToolExecutionStatus.Success)]
    [InlineData(ToolExecutionStatus.Failure)]
    public async Task AuditSinkFailuresDoNotChangeAllowedExecution(ToolExecutionStatus status)
    {
        var router = new FakeRouter(new ToolExecutionResult(status, "SUPER_SECRET_DIRECT_RESULT"));
        var gateway = Gateway(new ThrowingTrail(), router);

        var result = await gateway.ExecuteAsync(Request("read"));

        Assert.Equal(status, result.Status);
        Assert.Equal(1, router.Calls);
    }

    [Fact]
    public async Task AuditSinkFailuresDoNotChangePolicyBlock()
    {
        var router = new FakeRouter(ToolExecutionResult.Success("unexpected"));
        var gateway = Gateway(new ThrowingTrail(), router, new TestTool("write", new(false, true, false, ExecutionRiskLevel.High)));

        var result = await gateway.ExecuteAsync(Request("write"));

        Assert.Equal(ToolExecutionStatus.Unauthorized, result.Status);
        Assert.Equal(0, router.Calls);
    }

    [Fact]
    public async Task ConcurrentDirectExecutionsHaveIsolatedFlows()
    {
        var trail = Trail();
        var gateway = Gateway(trail, new FakeRouter(ToolExecutionResult.Success("ok")));

        await Task.WhenAll(gateway.ExecuteAsync(Request("read")), gateway.ExecuteAsync(Request("read")));
        var events = await trail.GetSnapshotAsync();
        var flows = events.GroupBy(item => item.FlowId).ToArray();

        Assert.Equal(2, flows.Length);
        Assert.All(flows, flow => Assert.Equal(new[] { AuditEventType.DirectReadOnlyExecutionStarted, AuditEventType.DirectReadOnlyExecutionCompleted }, flow.Select(item => item.EventType)));
    }

    private static InMemoryExecutionAuditTrail Trail() => new(Options.Create(new ExecutionAuditOptions { MaxEvents = 32 }));

    private static ReadOnlyToolExecutionGateway Gateway(IExecutionAuditTrail trail, IToolRouter router, IKernelTool? tool = null) =>
        new(new DefaultExecutionPolicy(), new TestRegistry(tool ?? new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low))), router, new SafeExecutionAuditWriter(trail, NullLogger<SafeExecutionAuditWriter>.Instance), new TestTimeProvider());

    private static ReadOnlyToolExecutionGateway Gateway(ThrowingTrail trail, IToolRouter router, IKernelTool? tool = null) =>
        new(new DefaultExecutionPolicy(), new TestRegistry(tool ?? new TestTool("read", new(true, false, false, ExecutionRiskLevel.Low))), router, new SafeExecutionAuditWriter(trail, NullLogger<SafeExecutionAuditWriter>.Instance), new TestTimeProvider());

    private static ToolExecutionRequest Request(string toolName, string? value = null) => new(
        toolName,
        value is null
            ? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement(value) });
}
