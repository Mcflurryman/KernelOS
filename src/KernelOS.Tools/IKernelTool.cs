using KernelOS.Core;

namespace KernelOS.Tools;

public interface IKernelTool
{
    string Name { get; }

    string Description { get; }

    string Category { get; }

    IReadOnlyCollection<ToolCapability> Capabilities { get; }

    IReadOnlyCollection<ToolParameter> Parameters { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
