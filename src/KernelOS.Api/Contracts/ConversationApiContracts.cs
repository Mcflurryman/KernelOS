using System.Text.Json;
using KernelOS.Core.Kai;

namespace KernelOS.Api.Contracts;

public sealed record ConversationTurnApiRequest(string? Message, KaiMode PreferredMode = KaiMode.Auto, string? ToolName = null, IReadOnlyDictionary<string, JsonElement>? Arguments = null);
public sealed record ConversationApiResponse(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record ConversationMessageApiResponse(Guid Id, Guid ConversationId, long Sequence, string Role, string Content, DateTimeOffset CreatedAt);
public sealed record ConversationTurnApiResponse(Guid ConversationId, Guid? UserMessageId, Guid? AssistantMessageId, string TurnStatus, string? KaiStatus, string? ModeUsed, string? Answer, object? Citations, object? Warnings, string? Model, Guid? PendingExecutionId, object? Confirmation, string? ErrorCode);
