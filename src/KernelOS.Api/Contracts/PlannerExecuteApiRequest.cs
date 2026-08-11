using System.Text.Json;
namespace KernelOS.Api.Contracts;
public sealed record PlannerExecuteApiRequest(string? Goal, string? Tool, IReadOnlyDictionary<string, JsonElement>? Arguments, IReadOnlyDictionary<Guid, Guid>? ApprovalIds = null);
