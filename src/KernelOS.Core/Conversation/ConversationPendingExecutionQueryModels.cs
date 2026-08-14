using KernelOS.Core.Execution;

namespace KernelOS.Core.Conversation;

public enum ConversationPendingExecutionStatus
{
    Pending,
    Approved,
    Rejected,
    Unavailable
}

public sealed record ConversationPendingExecution(
    Guid PendingExecutionId,
    Guid UserMessageId,
    Guid? AssistantMessageId,
    DateTimeOffset CreatedAt,
    ConversationPendingExecutionStatus Status,
    ConversationPendingExecutionConfirmation? Confirmation = null);

public sealed record ConversationPendingExecutionConfirmation(
    string Description,
    ExecutionRiskLevel RiskLevel,
    ExecutionPolicyReason Reason,
    string SafeArgumentSummary,
    DateTimeOffset ExpiresAt,
    int TaskCount);

public enum ConversationPendingExecutionQueryStatus
{
    Success,
    NotFound,
    InvalidRequest,
    Cancelled,
    Failed
}

public sealed record ConversationPendingExecutionListResult(
    ConversationPendingExecutionQueryStatus Status,
    IReadOnlyList<ConversationPendingExecution>? PendingExecutions = null);

public interface IConversationPendingExecutionQueryService
{
    Task<ConversationPendingExecutionListResult> ListByConversationAsync(
        ConversationExecutionCorrelationListQuery query,
        CancellationToken cancellationToken = default);
}
