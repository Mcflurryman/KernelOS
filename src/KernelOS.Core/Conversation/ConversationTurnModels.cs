using KernelOS.Core.Kai;
using System.Text.Json;

namespace KernelOS.Core.Conversation;

public enum ConversationTurnStatus { Success, PartialSuccess, ConfirmationRequired, NoContext, Cancelled, Failed, NotFound, InvalidRequest }
public sealed record ConversationTurnRequest(Guid ConversationId, string? Message, KaiMode PreferredMode = KaiMode.Auto, string? ToolName = null, IReadOnlyDictionary<string, JsonElement>? Arguments = null);
public sealed record ConversationTurnResult(ConversationTurnStatus Status, Guid ConversationId, Guid? UserMessageId = null, Guid? AssistantMessageId = null, KaiResponse? KaiResponse = null, string? ErrorCode = null);
public interface IConversationTurnOrchestrator { Task<ConversationTurnResult> HandleAsync(ConversationTurnRequest request, CancellationToken cancellationToken = default); }
