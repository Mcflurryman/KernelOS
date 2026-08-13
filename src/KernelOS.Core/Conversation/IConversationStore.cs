namespace KernelOS.Core.Conversation;

public interface IConversationStore
{
    Task<ConversationCreateResult> CreateAsync(CancellationToken cancellationToken = default);
    Task<ConversationGetResult> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<ConversationListResult> ListAsync(ConversationListQuery query, CancellationToken cancellationToken = default);
    Task<ConversationAppendResult> AppendMessageAsync(AppendConversationMessageRequest request, CancellationToken cancellationToken = default);
    Task<ConversationMessagesResult> GetMessagesAsync(ConversationMessagesQuery query, CancellationToken cancellationToken = default);
    Task<ConversationDeleteResult> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
