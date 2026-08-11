using KernelOS.Core;
using KernelOS.Core.Execution;

namespace KernelOS.Tools;

public sealed class ReadOnlyToolExecutionGateway(
    IExecutionPolicy executionPolicy,
    IToolRegistry toolRegistry,
    IToolRouter toolRouter) : IReadOnlyToolExecutionGateway
{
    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ToolExecutionResult.Cancelled());
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ToolName) || request.Arguments is null)
        {
            return Task.FromResult(ToolExecutionResult.InvalidArguments("A tool name and arguments are required."));
        }

        var tool = toolRegistry.GetByName(request.ToolName);
        if (tool is null)
        {
            return Task.FromResult(ToolExecutionResult.NotFound("The requested tool is not registered."));
        }

        var metadata = tool.ExecutionMetadata;
        var decision = executionPolicy.Evaluate(new ExecutionPolicyRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            request.ToolName,
            metadata));

        return decision.Type == ExecutionPolicyDecisionType.Allow
            ? toolRouter.ExecuteAsync(request, cancellationToken)
            : Task.FromResult(new ToolExecutionResult(ToolExecutionStatus.Unauthorized, "Direct execution is restricted to tools allowed by the execution policy."));
    }
}
