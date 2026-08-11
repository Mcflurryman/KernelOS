using KernelOS.Core;
using KernelOS.Core.Execution;

namespace KernelOS.Tools;

public interface IKernelTool
{
    string Name { get; }

    string Description { get; }

    string Category { get; }

    IReadOnlyCollection<ToolCapability> Capabilities { get; }

    IReadOnlyCollection<ToolParameter> Parameters { get; }

    ToolExecutionMetadata ExecutionMetadata => ToolExecutionMetadata.Unknown;

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
