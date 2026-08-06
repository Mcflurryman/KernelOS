using KernelOS.Core;

namespace KernelOS.Tools;

public interface IToolRouter
{
    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
