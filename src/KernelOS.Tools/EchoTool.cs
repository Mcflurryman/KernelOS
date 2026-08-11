using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Execution;

namespace KernelOS.Tools;

public sealed class EchoTool : IKernelTool
{
    private static readonly IReadOnlyCollection<ToolCapability> ToolCapabilities =
        [new ToolCapability("echo", "Returns the provided text without modification.")];

    private static readonly IReadOnlyCollection<ToolParameter> ToolParameters =
        [new ToolParameter("text", "Text to return.", "string", true)];

    public string Name => "echo";

    public string Description => "Returns the provided text without modification.";

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

        if (!request.Arguments.TryGetValue("text", out var text)
            || text.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(text.GetString()))
        {
            return Task.FromResult(ToolExecutionResult.InvalidArguments("The 'text' argument is required."));
        }

        var value = text.GetString()!;
        return Task.FromResult(ToolExecutionResult.Success(
            "The text was returned.",
            JsonSerializer.SerializeToElement(new { text = value })));
    }
}
