using System.Text.Json;

namespace KernelOS.Core;

public sealed record ToolExecutionRequest(
    string ToolName,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    ToolExecutionContext? Context = null);
