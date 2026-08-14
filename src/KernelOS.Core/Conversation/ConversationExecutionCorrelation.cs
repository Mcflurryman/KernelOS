namespace KernelOS.Core.Conversation;

public sealed record ConversationExecutionCorrelation(
    Guid PendingExecutionId,
    Guid ConversationId,
    Guid UserMessageId,
    Guid? AssistantMessageId,
    DateTimeOffset CreatedAt);

public sealed record RegisterConversationExecutionCorrelationRequest(
    Guid PendingExecutionId,
    Guid ConversationId,
    Guid UserMessageId,
    Guid? AssistantMessageId = null);

public sealed record ConversationExecutionCorrelationListQuery(Guid ConversationId, int Limit = 50, int Offset = 0);

public enum ConversationExecutionCorrelationStatus
{
    Success,
    NotFound,
    InvalidRequest,
    Conflict,
    Cancelled,
    Failed
}

public sealed record ConversationExecutionCorrelationRegisterResult(
    ConversationExecutionCorrelationStatus Status,
    ConversationExecutionCorrelation? Correlation = null);

public sealed record ConversationExecutionCorrelationGetResult(
    ConversationExecutionCorrelationStatus Status,
    ConversationExecutionCorrelation? Correlation = null);

public sealed record ConversationExecutionCorrelationListResult(
    ConversationExecutionCorrelationStatus Status,
    IReadOnlyList<ConversationExecutionCorrelation>? Correlations = null);
