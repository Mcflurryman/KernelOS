namespace KernelOS.Core.Conversation;

public enum ConversationStatus { Success, NotFound, InvalidRequest, Cancelled, Failed }

public sealed record Conversation(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record ConversationMessage(Guid Id, Guid ConversationId, long Sequence, ConversationRole Role, string Content, DateTimeOffset CreatedAt);
public sealed record ConversationListQuery(int Limit = 50, int Offset = 0);
public sealed record ConversationMessagesQuery(Guid ConversationId, int Limit = 50, int Offset = 0, long? BeforeSequence = null);
public sealed record AppendConversationMessageRequest(Guid ConversationId, ConversationRole Role, string? Content);
public sealed record ConversationCreateResult(ConversationStatus Status, Conversation? Conversation = null);
public sealed record ConversationGetResult(ConversationStatus Status, Conversation? Conversation = null);
public sealed record ConversationListResult(ConversationStatus Status, IReadOnlyList<Conversation>? Conversations = null);
public sealed record ConversationAppendResult(ConversationStatus Status, Conversation? Conversation = null, ConversationMessage? Message = null);
public sealed record ConversationMessagesResult(ConversationStatus Status, IReadOnlyList<ConversationMessage>? Messages = null);
public sealed record ConversationDeleteResult(ConversationStatus Status);
