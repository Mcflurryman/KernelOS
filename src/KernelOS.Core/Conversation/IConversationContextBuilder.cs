namespace KernelOS.Core.Conversation;
public interface IConversationContextBuilder { Task<ConversationContextResult> BuildAsync(ConversationContextRequest request, CancellationToken cancellationToken = default); }
