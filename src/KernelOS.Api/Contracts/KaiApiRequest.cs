using System.Text.Json;
using KernelOS.Core.Kai;
namespace KernelOS.Api.Contracts;
public sealed record KaiApiRequest(string? Message, KaiMode PreferredMode = KaiMode.Auto, string? ToolName = null, IReadOnlyDictionary<string, JsonElement>? Arguments = null);
