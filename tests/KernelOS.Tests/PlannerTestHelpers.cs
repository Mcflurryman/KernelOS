using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Tools;

namespace KernelOS.Tests;

[CollectionDefinition("Side effect tool tests", DisableParallelization = true)]
public sealed class SideEffectToolTestSerialGroup;

internal static class PlannerTestData
{
    internal static Goal CreateGoal() => new(
        Guid.NewGuid(),
        "EJECUTAR Echo",
        DateTimeOffset.UtcNow,
        0,
        new Dictionary<string, JsonElement>
        {
            ["tool"] = JsonSerializer.SerializeToElement("echo"),
            ["arguments"] = JsonSerializer.SerializeToElement(new { text = "Hola" })
        });

    internal static Plan CreateMultiTaskPlan() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        [CreateTask(Guid.NewGuid(), "first"), CreateTask(Guid.NewGuid(), "second")],
        PlannerStatus.Planned,
        null,
        null);

    internal static PlanTask CreateTask(Guid id, string toolName) => new(
        id,
        "Execute requested tool",
        toolName,
        new Dictionary<string, JsonElement>(),
        PlannerStatus.Planned,
        0);
}

internal sealed class FakeRouter : IToolRouter
{
    private readonly Func<ToolExecutionRequest, ToolExecutionResult> execute;

    internal FakeRouter(ToolExecutionResult result)
        : this(_ => result)
    {
    }

    internal FakeRouter(Func<ToolExecutionRequest, ToolExecutionResult> execute) => this.execute = execute;

    internal int Calls { get; private set; }
    internal List<ToolExecutionRequest> Requests { get; } = [];

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        Requests.Add(request);
        return Task.FromResult(execute(request));
    }
}

internal sealed class AllowingGate : IExecutionGate
{
    public Task<ExecutionGateResult> EvaluateAsync(
        Guid planId,
        PlanTask task,
        Guid? approvalId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExecutionGateResult(
            ExecutionGateStatus.Authorized,
            new ExecutionPolicyDecision(
                ExecutionPolicyDecisionType.Allow,
                ExecutionRiskLevel.Low,
                ExecutionPolicyReason.ReadOnlyAllowed)));
}

internal sealed class BlockingGate(ExecutionGateStatus status) : IExecutionGate
{
    public Task<ExecutionGateResult> EvaluateAsync(
        Guid planId,
        PlanTask task,
        Guid? approvalId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExecutionGateResult(
            status,
            new ExecutionPolicyDecision(
                status == ExecutionGateStatus.Denied
                    ? ExecutionPolicyDecisionType.Deny
                    : ExecutionPolicyDecisionType.RequireConfirmation,
                ExecutionRiskLevel.High,
                status == ExecutionGateStatus.Denied
                    ? ExecutionPolicyReason.PolicyDenied
                    : ExecutionPolicyReason.SideEffectRequiresConfirmation)));
}
