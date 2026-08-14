using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;
using KernelOS.Core.Kai;
using KernelOS.Infrastructure.Conversation;
using Microsoft.Extensions.Logging.Abstractions;

namespace KernelOS.Tests;

public sealed class ConversationTurnOrchestratorTests
{
    [Fact]
    public async Task SameConversationSerializesUserHistoryKaiAndAssistant()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kai = new FakeKai(async (request, _) => { if (request.Message == "User1") { firstEntered.SetResult(); await release.Task; } return Success(request.Message!); });
        var orchestrator = Create(store, kai);

        var first = orchestrator.HandleAsync(new(id, "User1")); await firstEntered.Task;
        var second = orchestrator.HandleAsync(new(id, "User2")); await Task.Yield();

        Assert.Single(store.Messages); Assert.Equal(1, store.HistoryCalls); Assert.Single(kai.Requests);
        release.SetResult(); await Task.WhenAll(first, second);
        Assert.Equal(["User1", "Assistant1", "User2", "Assistant2"], store.Messages.Select(message => message.Content));
        Assert.Equal([1L, 2, 3, 4], store.Messages.Select(message => message.Sequence));
        Assert.Equal(0, orchestrator.ActiveGateCount);
    }

    [Fact]
    public async Task KeyedGateAllowsDifferentConversationsAndCleansUpAfterContention()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var store = new FakeStore(first, second); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kai = new FakeKai(async (request, _) => { if (request.Message == "A") { entered.SetResult(); await release.Task; } return Success(request.Message!); });
        var orchestrator = Create(store, kai);

        var blocked = orchestrator.HandleAsync(new(first, "A")); await entered.Task;
        var independent = await orchestrator.HandleAsync(new(second, "B"));
        Assert.Equal(ConversationTurnStatus.Success, independent.Status);
        Assert.True(kai.MaxGlobalConcurrent > 1 || kai.CompletedMessages.Contains("B"));

        release.SetResult(); await blocked; kai.ResetConcurrency();
        var turns = Enumerable.Range(0, 32).Select(index => orchestrator.HandleAsync(new(first, $"M{index}"))).ToArray();
        await Task.WhenAll(turns);
        Assert.Equal(1, kai.MaxConcurrentSameConversation);
        Assert.Equal(0, orchestrator.ActiveGateCount);
    }

    [Fact]
    public async Task WaitingTurnCancellationDoesNotTouchStoreOrKai()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kai = new FakeKai(async (request, _) => { entered.TrySetResult(); await release.Task; return Success(request.Message!); }); var orchestrator = Create(store, kai);
        var first = orchestrator.HandleAsync(new(id, "one")); await entered.Task;
        using var cancellation = new CancellationTokenSource(); var waiting = orchestrator.HandleAsync(new(id, "two"), cancellation.Token); cancellation.Cancel();
        Assert.Equal(ConversationTurnStatus.Cancelled, (await waiting).Status); Assert.Single(store.Messages); Assert.Single(kai.Requests);
        release.SetResult(); await first; Assert.Equal(0, orchestrator.ActiveGateCount);
    }

    [Fact]
    public async Task CancellationBeforeGateDoesNotCallDependencies()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var kai = new FakeKai((_, _) => Task.FromResult(Success("x"))); var orchestrator = Create(store, kai); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(ConversationTurnStatus.Cancelled, (await orchestrator.HandleAsync(new(id, "x"), cancellation.Token)).Status); Assert.Empty(store.Messages); Assert.Empty(kai.Requests);
    }

    [Theory]
    [InlineData(KaiStatus.Success, "answer", true, ConversationTurnStatus.Success)]
    [InlineData(KaiStatus.PartialSuccess, "answer", true, ConversationTurnStatus.PartialSuccess)]
    [InlineData(KaiStatus.Failed, "answer", false, ConversationTurnStatus.Failed)]
    [InlineData(KaiStatus.Cancelled, "answer", false, ConversationTurnStatus.Cancelled)]
    [InlineData(KaiStatus.RequiresConfirmation, "visible confirmation", true, ConversationTurnStatus.ConfirmationRequired)]
    public async Task PersistsOnlyEligibleVisibleAssistantResponses(KaiStatus status, string answer, bool persisted, ConversationTurnStatus expected)
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var result = await Create(store, new FakeKai((_, _) => Task.FromResult(new KaiResponse(status, KaiMode.Chat, answer, Warnings: [new("SUPER_SECRET_TOOL_INTERNAL", "secret")])))).HandleAsync(new(id, "user"));
        Assert.Equal(expected, result.Status); Assert.Equal(status, result.KaiResponse!.Status); Assert.Equal(persisted ? answer : "user", store.Messages.Last().Content);
        Assert.Equal(persisted ? 2 : 1, store.Messages.Count); Assert.DoesNotContain(store.Messages, message => message.Content.Contains("SUPER_SECRET_TOOL_INTERNAL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyAssistantIsNotAppendedAndIsReportedAsPartialPersistence()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var result = await Create(store, new FakeKai((_, _) => Task.FromResult(new KaiResponse(KaiStatus.Success, KaiMode.Chat, " ")))).HandleAsync(new(id, "user"));
        Assert.Equal(ConversationTurnStatus.PartialSuccess, result.Status); Assert.Equal("CONVERSATION_ASSISTANT_RESPONSE_INVALID", result.ErrorCode); Assert.Single(store.Messages); Assert.Null(result.AssistantMessageId);
    }

    [Fact]
    public async Task AssistantPersistenceFailureKeepsKaiResponseAndUserWithoutRetry()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id) { AssistantAppendStatus = ConversationStatus.NotFound }; var kai = new FakeKai((_, _) => Task.FromResult(Success("SAFE_TEXT")));
        var result = await Create(store, kai).HandleAsync(new(id, "user"));
        Assert.Equal(ConversationTurnStatus.PartialSuccess, result.Status); Assert.Equal(KaiStatus.Success, result.KaiResponse!.Status); Assert.Null(result.AssistantMessageId); Assert.Single(store.Messages); Assert.Single(kai.Requests);
    }

    [Fact]
    public async Task UserOrHistoryFailurePreventsKaiAndKeepsOnlyCommittedUser()
    {
        var id = Guid.NewGuid(); var missing = new FakeStore(id) { UserAppendStatus = ConversationStatus.NotFound }; var noKai = new FakeKai((_, _) => Task.FromResult(Success("x")));
        Assert.Equal(ConversationTurnStatus.NotFound, (await Create(missing, noKai).HandleAsync(new(id, "x"))).Status); Assert.Empty(noKai.Requests);
        var cancelledHistory = new FakeStore(id) { HistoryStatus = ConversationStatus.Cancelled }; var result = await Create(cancelledHistory, noKai).HandleAsync(new(id, "x"));
        Assert.Equal(ConversationTurnStatus.Cancelled, result.Status); Assert.Single(cancelledHistory.Messages); Assert.Empty(noKai.Requests);
    }

    [Fact]
    public async Task CurrentUserAppearsExactlyOnceAndHistoryIsOrderedAndIsolated()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var store = new FakeStore(a, b);
        store.Seed(a, "User1", "Assistant1", "User2", "Assistant2", "SECRET_A"); store.Seed(b, "SECRET_B");
        var kai = new FakeKai((request, _) => Task.FromResult(Success("answer")));
        await Create(store, kai).HandleAsync(new(a, "CURRENT_USER_UNIQUE")); await Create(store, kai).HandleAsync(new(b, "B current"));
        var requestA = kai.Requests.Single(request => request.Message == "CURRENT_USER_UNIQUE"); var requestB = kai.Requests.Single(request => request.Message == "B current");
        Assert.DoesNotContain(requestA.History!, turn => turn.Content == "CURRENT_USER_UNIQUE"); Assert.Equal(["User1", "Assistant1", "User2", "Assistant2", "SECRET_A"], requestA.History!.Select(turn => turn.Content));
        Assert.DoesNotContain(requestB.History!, turn => turn.Content == "SECRET_A"); Assert.DoesNotContain(requestA.History!, turn => turn.Content == "SECRET_B");
    }

    [Fact]
    public async Task ConfirmationRegistersExactUserAndNullAssistantWithoutCreatingAnotherPending()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var correlations = new FakeCorrelationStore(); var pendingId = Guid.NewGuid();
        var result = await Create(store, new FakeKai((_, _) => Task.FromResult(Confirmation(pendingId))), correlations).HandleAsync(new(id, "user"));

        Assert.Equal(ConversationTurnStatus.ConfirmationRequired, result.Status);
        Assert.Single(correlations.Registrations);
        Assert.Equal(pendingId, correlations.Registrations[0].PendingExecutionId);
        Assert.Equal(store.Messages.Single(message => message.Role == ConversationRole.User).Id, correlations.Registrations[0].UserMessageId);
        Assert.Null(correlations.Registrations[0].AssistantMessageId);
        Assert.Single(store.Messages);
    }

    [Fact]
    public async Task VisibleConfirmationRegistersExactPersistedAssistantAndNonConfirmationDoesNotRegister()
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var correlations = new FakeCorrelationStore(); var pendingId = Guid.NewGuid();
        var confirmed = await Create(store, new FakeKai((_, _) => Task.FromResult(Confirmation(pendingId, "confirmation"))), correlations).HandleAsync(new(id, "user"));
        var normal = await Create(store, new FakeKai((_, _) => Task.FromResult(Success("answer"))), correlations).HandleAsync(new(id, "next"));

        Assert.Equal(ConversationTurnStatus.ConfirmationRequired, confirmed.Status);
        Assert.Equal(ConversationTurnStatus.Success, normal.Status);
        Assert.Single(correlations.Registrations);
        Assert.Equal(confirmed.AssistantMessageId, correlations.Registrations[0].AssistantMessageId);
        Assert.Equal(store.Messages.Single(message => message.Id == confirmed.AssistantMessageId).Id, correlations.Registrations[0].AssistantMessageId);
    }

    [Theory]
    [InlineData(ConversationExecutionCorrelationStatus.Failed, "CONVERSATION_PENDING_CORRELATION_FAILED")]
    [InlineData(ConversationExecutionCorrelationStatus.Cancelled, "CONVERSATION_PENDING_CORRELATION_FAILED")]
    [InlineData(ConversationExecutionCorrelationStatus.Conflict, "CONVERSATION_PENDING_CORRELATION_CONFLICT")]
    public async Task CorrelationFailureOrConflictPreservesConfirmationAndAddsSafeWarning(ConversationExecutionCorrelationStatus registrationStatus, string expectedWarning)
    {
        var id = Guid.NewGuid(); var store = new FakeStore(id); var correlations = new FakeCorrelationStore { RegisterStatus = registrationStatus }; var pendingId = Guid.NewGuid();
        var result = await Create(store, new FakeKai((_, _) => Task.FromResult(Confirmation(pendingId))), correlations).HandleAsync(new(id, "user"));

        Assert.Equal(ConversationTurnStatus.ConfirmationRequired, result.Status);
        Assert.Equal(KaiStatus.RequiresConfirmation, result.KaiResponse!.Status);
        Assert.Equal(pendingId, result.KaiResponse.PendingExecutionId);
        Assert.Equal(expectedWarning, result.ErrorCode);
        Assert.Contains(result.KaiResponse.Warnings!, warning => warning.Code == expectedWarning);
        Assert.Single(correlations.Registrations);
    }

    private static ConversationTurnOrchestrator Create(FakeStore store, FakeKai kai, FakeCorrelationStore? correlations = null) => new(store, kai, correlations ?? new FakeCorrelationStore(), NullLogger<ConversationTurnOrchestrator>.Instance);
    private static KaiResponse Success(string message) => new(KaiStatus.Success, KaiMode.Chat, message.StartsWith("User", StringComparison.Ordinal) ? "Assistant" + message[4..] : "answer");
    private static KaiResponse Confirmation(Guid pendingExecutionId, string answer = "") => new(KaiStatus.RequiresConfirmation, KaiMode.Planner, answer, PendingExecutionId: pendingExecutionId, Confirmation: new(pendingExecutionId, Guid.NewGuid(), Guid.NewGuid(), "write", "Confirmation required.", ExecutionRiskLevel.High, ExecutionPolicyReason.SideEffectRequiresConfirmation, "Arguments are not displayed by default.", DateTimeOffset.UtcNow.AddMinutes(5)));

    private sealed class FakeKai(Func<KaiRequest, CancellationToken, Task<KaiResponse>> handle) : IKaiAgent
    {
        private int active; public List<KaiRequest> Requests { get; } = []; public List<string> CompletedMessages { get; } = []; public int MaxConcurrentSameConversation { get; private set; } public int MaxGlobalConcurrent { get; private set; }
        public async Task<KaiResponse> HandleAsync(KaiRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            lock (Requests) Requests.Add(request); var now = Interlocked.Increment(ref active); MaxGlobalConcurrent = Math.Max(MaxGlobalConcurrent, now); MaxConcurrentSameConversation = Math.Max(MaxConcurrentSameConversation, now);
            try { var result = await handle(request, cancellationToken); lock (Requests) CompletedMessages.Add(request.Message!); return result; } finally { Interlocked.Decrement(ref active); }
        }
        public void ResetConcurrency() { MaxConcurrentSameConversation = 0; MaxGlobalConcurrent = 0; }
    }

    private sealed class FakeStore(params Guid[] conversations) : IConversationStore
    {
        private readonly HashSet<Guid> existing = [.. conversations]; public List<ConversationMessage> Messages { get; } = []; public int HistoryCalls { get; private set; } public ConversationStatus UserAppendStatus { get; init; } = ConversationStatus.Success; public ConversationStatus AssistantAppendStatus { get; init; } = ConversationStatus.Success; public ConversationStatus HistoryStatus { get; init; } = ConversationStatus.Success;
        public Task<ConversationCreateResult> CreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ConversationCreateResult(ConversationStatus.Failed));
        public Task<ConversationGetResult> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(existing.Contains(id) ? new ConversationGetResult(ConversationStatus.Success, new Conversation(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1)) : new ConversationGetResult(ConversationStatus.NotFound));
        public Task<ConversationListResult> ListAsync(ConversationListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationListResult(ConversationStatus.Success, []));
        public Task<ConversationAppendResult> AppendMessageAsync(AppendConversationMessageRequest request, CancellationToken cancellationToken = default)
        {
            var status = request.Role == ConversationRole.User ? UserAppendStatus : AssistantAppendStatus; if (status != ConversationStatus.Success) return Task.FromResult(new ConversationAppendResult(status));
            var message = new ConversationMessage(Guid.NewGuid(), request.ConversationId, Messages.Count(item => item.ConversationId == request.ConversationId) + 1, request.Role, request.Content!, DateTimeOffset.UtcNow); Messages.Add(message); return Task.FromResult(new ConversationAppendResult(ConversationStatus.Success, Message: message));
        }
        public Task<ConversationMessagesResult> GetMessagesAsync(ConversationMessagesQuery query, CancellationToken cancellationToken = default)
        {
            HistoryCalls++; if (HistoryStatus != ConversationStatus.Success) return Task.FromResult(new ConversationMessagesResult(HistoryStatus)); var values = Messages.Where(message => message.ConversationId == query.ConversationId && (query.BeforeSequence is null || message.Sequence < query.BeforeSequence)).OrderBy(message => message.Sequence).TakeLast(query.Limit).ToArray(); return Task.FromResult(new ConversationMessagesResult(ConversationStatus.Success, values));
        }
        public Task<ConversationDeleteResult> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationDeleteResult(ConversationStatus.Success));
        public void Seed(Guid id, params string[] content) { foreach (var value in content) { var role = value.StartsWith("Assistant", StringComparison.Ordinal) ? ConversationRole.Assistant : ConversationRole.User; Messages.Add(new(Guid.NewGuid(), id, Messages.Count(message => message.ConversationId == id) + 1, role, value, DateTimeOffset.UtcNow)); } }
    }

    private sealed class FakeCorrelationStore : IConversationExecutionCorrelationStore
    {
        public List<RegisterConversationExecutionCorrelationRequest> Registrations { get; } = [];
        public ConversationExecutionCorrelationStatus RegisterStatus { get; init; } = ConversationExecutionCorrelationStatus.Success;
        public Task<ConversationExecutionCorrelationRegisterResult> RegisterAsync(RegisterConversationExecutionCorrelationRequest request, CancellationToken cancellationToken = default)
        {
            Registrations.Add(request);
            var correlation = new ConversationExecutionCorrelation(request.PendingExecutionId, request.ConversationId, request.UserMessageId, request.AssistantMessageId, DateTimeOffset.UtcNow);
            return Task.FromResult(new ConversationExecutionCorrelationRegisterResult(RegisterStatus, RegisterStatus == ConversationExecutionCorrelationStatus.Success ? correlation : null));
        }
        public Task<ConversationExecutionCorrelationGetResult> GetByPendingExecutionIdAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationExecutionCorrelationGetResult(ConversationExecutionCorrelationStatus.NotFound));
        public Task<ConversationExecutionCorrelationListResult> ListByConversationAsync(ConversationExecutionCorrelationListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationExecutionCorrelationListResult(ConversationExecutionCorrelationStatus.Success, []));
    }
}
