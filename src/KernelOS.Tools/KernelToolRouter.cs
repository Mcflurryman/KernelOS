using KernelOS.Core;
using Microsoft.Extensions.Logging;

namespace KernelOS.Tools;

public sealed class KernelToolRouter(
    IToolRegistry toolRegistry,
    ILogger<KernelToolRouter> logger) : IToolRouter
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

        try
        {
            return await tool.ExecuteAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ToolExecutionResult.Cancelled();
        }
        catch (ArgumentException)
        {
            return ToolExecutionResult.InvalidArguments("The tool arguments are invalid.");
        }
        catch (Exception exception)
        {
            KernelToolRouterLog.UnexpectedError(logger, exception, tool.Name);
            return ToolExecutionResult.Failure();
        }
    }
}

internal static partial class KernelToolRouterLog
{
    [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "Tool '{ToolName}' failed unexpectedly.")]
    public static partial void UnexpectedError(ILogger logger, Exception exception, string toolName);
}
