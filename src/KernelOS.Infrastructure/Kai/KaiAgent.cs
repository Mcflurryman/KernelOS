using System.Text.Json;
using KernelOS.Core;
using KernelOS.Core.Audit;
using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;
using KernelOS.Core.Kai;
using KernelOS.Core.Planning;
using KernelOS.Core.Rag;
using KernelOS.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Kai;

public sealed class KaiAgent(
    IConversationContextBuilder conversation,
    IKaiIntentRouter router,
    IRagPipeline rag,
    IChatModel chat,
    IPlanner planner,
    IPlanExecutor executor,
    IExecutionConfirmationService confirmations,
    IOptions<KaiOptions> options,
    IExecutionAuditWriter? audit = null,
    TimeProvider? timeProvider = null) : IKaiAgent
{
    private readonly KaiOptions options = options.Value;

    public async Task<KaiResponse> HandleAsync(KaiRequest request, CancellationToken cancellationToken = default)
    {
        var auditContext = new ExecutionAuditContext(AuditFlowId.Create(), ExecutionOrigin.Kai);
        var clock = timeProvider ?? TimeProvider.System;
        _ = audit?.WriteAsync(new AuditEvent(auditContext.FlowId, clock.GetUtcNow(), AuditEventType.KaiRequestStarted, Origin: auditContext.Origin), CancellationToken.None);
        if (cancellationToken.IsCancellationRequested) return new(KaiStatus.Cancelled, KaiMode.Auto);
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > options.MaxMessageCharacters || !Enum.IsDefined(request.PreferredMode)) return new(KaiStatus.InvalidRequest, KaiMode.Auto);

        var context = await conversation.BuildAsync(new(request.History ?? [], request.Message), cancellationToken);
        if (context.Status is ConversationContextStatus.Cancelled) return new(KaiStatus.Cancelled, KaiMode.Auto);
        if (context.Status is ConversationContextStatus.InvalidRequest or ConversationContextStatus.Failed) return new(KaiStatus.Failed, KaiMode.Auto);

        var decision = request.PreferredMode == KaiMode.Auto && !string.IsNullOrWhiteSpace(request.ToolName)
            ? new KaiDecision(KaiMode.Planner, "EXPLICIT_ACTION")
            : router.Route(request.Message, request.PreferredMode);
        _ = audit?.WriteAsync(new AuditEvent(auditContext.FlowId, clock.GetUtcNow(), AuditEventType.KaiRouteSelected, Origin: auditContext.Origin, Status: decision.Mode.ToString()), CancellationToken.None);

        return decision.Mode switch
        {
            KaiMode.Planner => await PlanAsync(request, decision, auditContext, cancellationToken),
            KaiMode.Rag => await RagAsync(request, decision, context.Pack, cancellationToken),
            _ => await ChatAsync(request, decision, context.Pack, cancellationToken)
        };
    }

    private async Task<KaiResponse> PlanAsync(KaiRequest request, KaiDecision decision, ExecutionAuditContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName)) return new(KaiStatus.PlanningFailed, KaiMode.Planner, Warnings: [new("KAI_PLANNER_INPUT_REQUIRED", "An explicit supported tool is required.")], Decision: decision);
        var metadata = new Dictionary<string, JsonElement> { ["tool"] = JsonSerializer.SerializeToElement(request.ToolName), ["arguments"] = JsonSerializer.SerializeToElement(request.Arguments ?? new Dictionary<string, JsonElement>()) };
        var built = await planner.PlanAsync(new Goal(Guid.NewGuid(), "EJECUTAR", DateTimeOffset.UtcNow, 0, metadata, context), cancellationToken);
        if (built.Status != PlannerStatus.Planned || built.Plan is null) return new(built.Status == PlannerStatus.Cancelled ? KaiStatus.Cancelled : KaiStatus.PlanningFailed, KaiMode.Planner, Decision: decision);

        var execution = await executor.ExecuteAsync(built.Plan, null, cancellationToken);
        if (execution.Status == PlannerStatus.RequiresConfirmation)
        {
            var pending = await confirmations.CreatePendingAsync(built.Plan, built.Plan.Tasks.Single().Id, cancellationToken);
            if (pending.Confirmation is null) return new(KaiStatus.PlanningFailed, KaiMode.Planner, Warnings: [new("KAI_PLANNER_UNSUPPORTED_TOOL", "The requested tool cannot be confirmed or executed.")], Decision: decision, PlanId: built.Plan.Id, Execution: execution);
            return new(KaiStatus.RequiresConfirmation, KaiMode.Planner, Decision: decision, PlanId: built.Plan.Id, PendingExecutionId: pending.Confirmation.PendingExecutionId, Confirmation: pending.Confirmation, Execution: execution);
        }

        return new(execution.Status == PlannerStatus.Completed ? KaiStatus.Completed : execution.Status == PlannerStatus.Denied ? KaiStatus.Denied : execution.Status == PlannerStatus.Cancelled ? KaiStatus.Cancelled : KaiStatus.Failed, KaiMode.Planner, Decision: decision, PlanId: built.Plan.Id, Execution: execution);
    }

    private async Task<KaiResponse> RagAsync(KaiRequest request, KaiDecision decision, ConversationContextPack? pack, CancellationToken cancellationToken)
    {
        var result = await rag.AnswerAsync(new(request.Message, History: ToChatHistory(pack)), cancellationToken);
        if (result.Status == RagStatus.NoContext && request.PreferredMode == KaiMode.Auto && options.AllowAutoRagFallbackToChat)
        {
            var fallback = await ChatAsync(request, decision, pack, cancellationToken);
            return fallback with { Warnings = [new("KAI_RAG_NO_CONTEXT_FALLBACK", "No retrieved context; used chat.")] };
        }
        var status = result.Status switch
        {
            RagStatus.Success => KaiStatus.Success,
            RagStatus.PartialSuccess => KaiStatus.PartialSuccess,
            RagStatus.NoContext => KaiStatus.NoContext,
            RagStatus.InvalidRequest => KaiStatus.InvalidRequest,
            RagStatus.ProviderUnavailable => KaiStatus.ProviderUnavailable,
            RagStatus.Cancelled => KaiStatus.Cancelled,
            _ => KaiStatus.Failed
        };
        var warnings = result.Warnings?.Select(warning => new KaiWarning(warning.Code, warning.Message)).ToArray();
        return new(status, KaiMode.Rag, result.Answer, result.Citations, warnings, result.Model, decision);
    }

    private async Task<KaiResponse> ChatAsync(KaiRequest request, KaiDecision decision, ConversationContextPack? pack, CancellationToken cancellationToken)
    {
        var response = await chat.SendAsync(new ChatRequest(request.Message!, history: ToChatHistory(pack)), cancellationToken);
        return new(response.Success ? KaiStatus.Success : KaiStatus.ProviderUnavailable, KaiMode.Chat, response.Message, Decision: decision);
    }
    private static ChatMessage[] ToChatHistory(ConversationContextPack? pack) => pack?.Items.Select(item => new ChatMessage(item.Role == ConversationRole.User ? "user" : "assistant", item.Content)).ToArray() ?? [];
}
