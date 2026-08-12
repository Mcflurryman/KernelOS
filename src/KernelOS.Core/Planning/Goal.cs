using System.Text.Json;
namespace KernelOS.Core.Planning;
public sealed record Goal(Guid Id, string UserRequest, DateTimeOffset Timestamp, int Priority, IReadOnlyDictionary<string, JsonElement>? Metadata = null, Audit.ExecutionAuditContext? AuditContext = null);
