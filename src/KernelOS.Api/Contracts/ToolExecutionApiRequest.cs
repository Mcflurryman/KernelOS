using System.Text.Json;

namespace KernelOS.Api.Contracts;

public sealed record ToolExecutionApiRequest(IReadOnlyDictionary<string, JsonElement>? Arguments);
