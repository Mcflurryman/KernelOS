using KernelOS.Core;

namespace KernelOS.Tools;

public interface IReadOnlyToolExecutionGateway
{
    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
