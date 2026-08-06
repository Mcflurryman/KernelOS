using System.Text.Json;
using KernelOS.Core;
namespace KernelOS.Core.Planning;
public sealed record PlanTask(Guid Id, string Name, string ToolName, IReadOnlyDictionary<string, JsonElement> Arguments, PlannerStatus Status, int RetryCount, ToolExecutionResult? Result = null);
