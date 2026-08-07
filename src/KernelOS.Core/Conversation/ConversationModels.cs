namespace KernelOS.Core.Conversation;

public enum ConversationRole { User, Assistant }
public enum ConversationContextStatus { Success, PartialSuccess, NoContext, InvalidRequest, Cancelled, Failed }
public sealed record ConversationTurn(Guid Id, ConversationRole Role, string Content, DateTimeOffset CreatedAt);
public sealed record ConversationContextWarning(string Code, string Message);
public sealed record ConversationContextOptionsSnapshot(int DefaultMaxTokens, int MaxAllowedTokens, int DefaultMaxTurns, int MaxAllowedTurns, float CharactersPerTokenEstimate);
public sealed record ConversationContextRequest(IReadOnlyList<ConversationTurn>? History, string? CurrentUserMessage = null, int? MaxTokens = null, int? MaxTurns = null);
public sealed record ConversationContextItem(Guid TurnId, ConversationRole Role, string Content, int Order, int EstimatedTokens, DateTimeOffset CreatedAt);
public sealed record ConversationContextPack(IReadOnlyList<ConversationContextItem> Items, int EstimatedTokens, int MaxTokens, bool Truncated, IReadOnlyList<ConversationContextWarning>? Warnings = null);
public sealed record ConversationContextResult(ConversationContextStatus Status, ConversationContextPack? Pack = null, IReadOnlyList<ConversationContextWarning>? Warnings = null, string? Error = null);
