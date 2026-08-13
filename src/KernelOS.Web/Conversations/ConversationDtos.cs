using System.Text.Json;

namespace KernelOS.Web.Conversations;

public sealed record ConversationSummaryDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);

public sealed record ConversationMessageDto(Guid Id, Guid ConversationId, long Sequence, string Role, string Content, DateTimeOffset CreatedAt);

public sealed record SendTurnRequestDto(string Message);

public sealed record ConversationTurnDto(
    Guid ConversationId,
    Guid? UserMessageId,
    Guid? AssistantMessageId,
    string? TurnStatus,
    string? KaiStatus,
    string? ModeUsed,
    string? Answer,
    JsonElement? Citations,
    JsonElement? Warnings,
    string? Model,
    Guid? PendingExecutionId,
    ConfirmationDto? Confirmation,
    string? ErrorCode);

public sealed record ConfirmationDto(Guid PendingExecutionId, string? Description, string? RiskLevel, string? Reason, string? SafeArgumentSummary, DateTimeOffset? ExpiresAt);

public enum ConversationApiStatus
{
    Success,
    PartialSuccess,
    ConfirmationRequired,
    BadRequest,
    NotFound,
    ServerError,
    Uncertain,
    InvalidResponse,
    Cancelled
}

public sealed record ConversationApiResult<T>(ConversationApiStatus Status, T? Value = default)
{
    public bool IsSuccess => Status == ConversationApiStatus.Success;
}
