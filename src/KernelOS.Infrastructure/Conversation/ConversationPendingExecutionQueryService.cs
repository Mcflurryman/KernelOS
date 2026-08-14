using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;

namespace KernelOS.Infrastructure.Conversation;

public sealed class ConversationPendingExecutionQueryService(
    IConversationExecutionCorrelationStore correlations,
    IExecutionConfirmationService confirmations) : IConversationPendingExecutionQueryService
{
    public async Task<ConversationPendingExecutionListResult> ListByConversationAsync(
        ConversationExecutionCorrelationListQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await correlations.ListByConversationAsync(query, cancellationToken);
        if (result.Status != ConversationExecutionCorrelationStatus.Success)
        {
            return new(Map(result.Status));
        }

        var values = new List<ConversationPendingExecution>();
        foreach (var correlation in result.Correlations!)
        {
            var confirmation = await confirmations.GetAsync(correlation.PendingExecutionId, cancellationToken);
            values.Add(new(
                correlation.PendingExecutionId,
                correlation.UserMessageId,
                correlation.AssistantMessageId,
                correlation.CreatedAt,
                Map(confirmation?.Status),
                ToPublicConfirmation(confirmation?.Confirmation)));
        }

        return new(ConversationPendingExecutionQueryStatus.Success, Array.AsReadOnly(values.ToArray()));
    }

    private static ConversationPendingExecutionQueryStatus Map(ConversationExecutionCorrelationStatus status) => status switch
    {
        ConversationExecutionCorrelationStatus.NotFound => ConversationPendingExecutionQueryStatus.NotFound,
        ConversationExecutionCorrelationStatus.InvalidRequest => ConversationPendingExecutionQueryStatus.InvalidRequest,
        ConversationExecutionCorrelationStatus.Cancelled => ConversationPendingExecutionQueryStatus.Cancelled,
        _ => ConversationPendingExecutionQueryStatus.Failed
    };

    private static ConversationPendingExecutionStatus Map(PendingExecutionStatus? status) => status switch
    {
        PendingExecutionStatus.PendingConfirmation or PendingExecutionStatus.Executing => ConversationPendingExecutionStatus.Pending,
        PendingExecutionStatus.Approved => ConversationPendingExecutionStatus.Approved,
        PendingExecutionStatus.Rejected => ConversationPendingExecutionStatus.Rejected,
        _ => ConversationPendingExecutionStatus.Unavailable
    };

    private static ConversationPendingExecutionConfirmation? ToPublicConfirmation(ExecutionConfirmationRequest? confirmation) => confirmation is null ? null : new(
        confirmation.Description,
        confirmation.RiskLevel,
        confirmation.Reason,
        confirmation.SafeArgumentSummary,
        confirmation.ExpiresAt,
        confirmation.TaskCount);
}
