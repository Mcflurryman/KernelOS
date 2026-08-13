using KernelOS.Core.Conversation;
using KernelOS.Core.Kai;
using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure.Conversation;

public sealed class ConversationTurnOrchestrator(IConversationStore store, IKaiAgent kai, ILogger<ConversationTurnOrchestrator> logger) : IConversationTurnOrchestrator
{
    private readonly Dictionary<Guid, GateEntry> gates = [];
    private readonly object gatesLock = new();

    public async Task<ConversationTurnResult> HandleAsync(ConversationTurnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Message) || !Enum.IsDefined(request.PreferredMode)) return new(ConversationTurnStatus.InvalidRequest, request.ConversationId, ErrorCode: "CONVERSATION_TURN_INVALID");
        GateLease? lease = null;
        try
        {
            lease = await AcquireAsync(request.ConversationId, cancellationToken);
            var existing = await store.GetAsync(request.ConversationId, cancellationToken);
            if (existing.Status != ConversationStatus.Success) return new(Map(existing.Status), request.ConversationId, ErrorCode: "CONVERSATION_UNAVAILABLE");
            var user = await store.AppendMessageAsync(new(request.ConversationId, ConversationRole.User, request.Message), cancellationToken);
            if (user.Status != ConversationStatus.Success) return new(Map(user.Status), request.ConversationId, ErrorCode: "CONVERSATION_USER_PERSISTENCE_FAILED");
            var history = await store.GetMessagesAsync(new(request.ConversationId, 100, 0, user.Message!.Sequence), cancellationToken);
            if (history.Status != ConversationStatus.Success) return new(Map(history.Status), request.ConversationId, user.Message.Id, ErrorCode: "CONVERSATION_HISTORY_UNAVAILABLE");
            var turns = history.Messages!.Select(message => new ConversationTurn(message.Id, message.Role, message.Content, message.CreatedAt)).ToArray();
            var response = await kai.HandleAsync(new(request.Message, turns, request.PreferredMode, request.ToolName, request.Arguments), cancellationToken);
            if (!ShouldPersist(response))
            {
                return response.Status is KaiStatus.Success or KaiStatus.PartialSuccess
                    ? new(ConversationTurnStatus.PartialSuccess, request.ConversationId, user.Message.Id, KaiResponse: response, ErrorCode: "CONVERSATION_ASSISTANT_RESPONSE_INVALID")
                    : new(Map(response.Status), request.ConversationId, user.Message.Id, KaiResponse: response);
            }
            var assistant = await store.AppendMessageAsync(new(request.ConversationId, ConversationRole.Assistant, response.Answer), cancellationToken);
            if (assistant.Status != ConversationStatus.Success)
            {
                ConversationTurnLog.AssistantPersistenceFailed(logger);
                return new(ConversationTurnStatus.PartialSuccess, request.ConversationId, user.Message.Id, KaiResponse: response, ErrorCode: "CONVERSATION_ASSISTANT_PERSISTENCE_FAILED");
            }
            return new(Map(response.Status), request.ConversationId, user.Message.Id, assistant.Message!.Id, response);
        }
        catch (OperationCanceledException) { return new(ConversationTurnStatus.Cancelled, request.ConversationId); }
        catch { ConversationTurnLog.Failed(logger); return new(ConversationTurnStatus.Failed, request.ConversationId, ErrorCode: "CONVERSATION_TURN_FAILED"); }
        finally { lease?.Dispose(); }
    }

    internal int ActiveGateCount { get { lock (gatesLock) return gates.Count; } }

    private static bool ShouldPersist(KaiResponse response) => IsAssistantEligible(response) && !string.IsNullOrWhiteSpace(response.Answer);
    private static bool IsAssistantEligible(KaiResponse response) => response.Status is KaiStatus.Success or KaiStatus.PartialSuccess or KaiStatus.RequiresConfirmation;
    private static ConversationTurnStatus Map(ConversationStatus status) => status switch { ConversationStatus.NotFound => ConversationTurnStatus.NotFound, ConversationStatus.InvalidRequest => ConversationTurnStatus.InvalidRequest, ConversationStatus.Cancelled => ConversationTurnStatus.Cancelled, _ => ConversationTurnStatus.Failed };
    private static ConversationTurnStatus Map(KaiStatus status) => status switch { KaiStatus.Success or KaiStatus.Completed => ConversationTurnStatus.Success, KaiStatus.PartialSuccess => ConversationTurnStatus.PartialSuccess, KaiStatus.RequiresConfirmation => ConversationTurnStatus.ConfirmationRequired, KaiStatus.NoContext => ConversationTurnStatus.NoContext, KaiStatus.Cancelled => ConversationTurnStatus.Cancelled, _ => ConversationTurnStatus.Failed };
    private async Task<GateLease> AcquireAsync(Guid id, CancellationToken token)
    {
        GateEntry entry;
        lock (gatesLock)
        {
            if (!gates.TryGetValue(id, out entry!)) gates[id] = entry = new GateEntry();
            entry.Users++;
        }
        try { await entry.Gate.WaitAsync(token); return new GateLease(this, id, entry); }
        catch { Release(id, entry, false); throw; }
    }
    private void Release(Guid id, GateEntry entry, bool releaseGate)
    {
        if (releaseGate) entry.Gate.Release();
        lock (gatesLock)
        {
            entry.Users--;
            if (entry.Users == 0 && gates.TryGetValue(id, out var current) && ReferenceEquals(current, entry))
            {
                gates.Remove(id);
            }
        }
    }
#pragma warning disable CA1001 // Entries are removed only after all holders and waiters release; disposing the semaphore would reintroduce a use-after-remove race.
    private sealed class GateEntry { internal readonly SemaphoreSlim Gate = new(1, 1); internal int Users; }
#pragma warning restore CA1001
    private sealed class GateLease(ConversationTurnOrchestrator owner, Guid id, GateEntry entry) : IDisposable { public void Dispose() => owner.Release(id, entry, true); }
}

internal static partial class ConversationTurnLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation assistant persistence failed.")]
    internal static partial void AssistantPersistenceFailed(ILogger logger);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation turn failed.")]
    internal static partial void Failed(ILogger logger);
}
