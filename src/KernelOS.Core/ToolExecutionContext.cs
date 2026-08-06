using System.Text.Json;

namespace KernelOS.Core;

public sealed record ToolExecutionContext(
    string? CorrelationId = null,
    IReadOnlyDictionary<string, JsonElement>? Metadata = null);
