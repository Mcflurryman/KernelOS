using KernelOS.Core;
using KernelOS.Core.Audit;
using KernelOS.Core.Execution;

namespace KernelOS.Tools;

public sealed class ReadOnlyToolExecutionGateway(
    IExecutionPolicy executionPolicy,
    IToolRegistry toolRegistry,
    IToolRouter toolRouter,
    IExecutionAuditWriter? audit = null,
    TimeProvider? timeProvider = null) : IReadOnlyToolExecutionGateway
{
    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Cancelled();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ToolName) || request.Arguments is null)
        {
            return ToolExecutionResult.InvalidArguments("A tool name and arguments are required.");
        }

        var tool = toolRegistry.GetByName(request.ToolName);
        if (tool is null)
        {
            return ToolExecutionResult.NotFound("The requested tool is not registered.");
        }

        var metadata = tool.ExecutionMetadata;
        var decision = executionPolicy.Evaluate(new ExecutionPolicyRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            request.ToolName,
            metadata));

        if (decision.Type != ExecutionPolicyDecisionType.Allow)
        {
            return new ToolExecutionResult(ToolExecutionStatus.Unauthorized, "Direct execution is restricted to tools allowed by the execution policy.");
        }

        var auditContext = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.DirectReadOnly);
        var clock = timeProvider ?? TimeProvider.System;
        var executionTimestamp = clock.GetTimestamp();
        _ = audit?.WriteAsync(new AuditEvent(auditContext.FlowId, clock.GetUtcNow(), AuditEventType.DirectReadOnlyExecutionStarted, Origin: auditContext.Origin), CancellationToken.None);
        try
        {
            var result = await toolRouter.ExecuteAsync(request, cancellationToken);
            if (result.Status == ToolExecutionStatus.Success)
            {
                WriteTerminal(AuditEventType.DirectReadOnlyExecutionCompleted, result.Status);
            }
            else if (result.Status != ToolExecutionStatus.Cancelled)
            {
                WriteTerminal(AuditEventType.DirectReadOnlyExecutionFailed, result.Status);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // There is no direct read-only cancellation event in v1. Preserve the
            // functional cancellation without misclassifying it as a failure.
            throw;
        }
        catch
        {
            WriteTerminal(AuditEventType.DirectReadOnlyExecutionFailed, ToolExecutionStatus.Failure);
            throw;
        }

        void WriteTerminal(AuditEventType eventType, ToolExecutionStatus status) =>
            _ = audit?.WriteAsync(new AuditEvent(auditContext.FlowId, clock.GetUtcNow(), eventType, Origin: auditContext.Origin, Status: status.ToString(), Duration: clock.GetElapsedTime(executionTimestamp)), CancellationToken.None);
    }
}
