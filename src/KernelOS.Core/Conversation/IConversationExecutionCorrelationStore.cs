namespace KernelOS.Core.Conversation;

public interface IConversationExecutionCorrelationStore
{
    Task<ConversationExecutionCorrelationRegisterResult> RegisterAsync(
        RegisterConversationExecutionCorrelationRequest request,
        CancellationToken cancellationToken = default);

    Task<ConversationExecutionCorrelationGetResult> GetByPendingExecutionIdAsync(
        Guid pendingExecutionId,
        CancellationToken cancellationToken = default);

    Task<ConversationExecutionCorrelationListResult> ListByConversationAsync(
        ConversationExecutionCorrelationListQuery query,
        CancellationToken cancellationToken = default);
}
