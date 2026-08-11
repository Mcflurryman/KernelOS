using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;

namespace KernelOS.Tools;

public sealed class TimeTool : IKernelTool
{
    private static readonly IReadOnlyCollection<ToolCapability> ToolCapabilities =
        [new ToolCapability("local-time", "Reads the current local date and time.")];

    private static readonly IReadOnlyCollection<ToolParameter> ToolParameters = Array.Empty<ToolParameter>();

    public string Name => "time";

    public string Description => "Returns the current local date and time.";

    public string Category => "demonstration";

    public IReadOnlyCollection<ToolCapability> Capabilities => ToolCapabilities;

    public IReadOnlyCollection<ToolParameter> Parameters => ToolParameters;

    public ToolExecutionMetadata ExecutionMetadata => new(true, false, false, ExecutionRiskLevel.Low);

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ToolExecutionResult.Cancelled());
        }

        var localTime = DateTimeOffset.Now;
        return Task.FromResult(ToolExecutionResult.Success(
            "The local time was read.",
            JsonSerializer.SerializeToElement(new { localTime })));
    }
}
