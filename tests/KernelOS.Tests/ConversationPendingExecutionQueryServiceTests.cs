using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;
using KernelOS.Infrastructure.Conversation;

namespace KernelOS.Tests;

public sealed class ConversationPendingExecutionQueryServiceTests
{
    [Theory]
    [InlineData(PendingExecutionStatus.PendingConfirmation, ConversationPendingExecutionStatus.Pending)]
    [InlineData(PendingExecutionStatus.Executing, ConversationPendingExecutionStatus.Pending)]
    [InlineData(PendingExecutionStatus.Approved, ConversationPendingExecutionStatus.Approved)]
    [InlineData(PendingExecutionStatus.Rejected, ConversationPendingExecutionStatus.Rejected)]
    public async Task CurrentConfirmationStateIsMappedWithoutGivingCorrelationAuthority(PendingExecutionStatus source, ConversationPendingExecutionStatus expected)
    {
        var correlation = new ConversationExecutionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var service = new ConversationPendingExecutionQueryService(new CorrelationStore(correlation), new ConfirmationService(correlation.PendingExecutionId, source));

        var result = await service.ListByConversationAsync(new(correlation.ConversationId));

        Assert.Equal(ConversationPendingExecutionQueryStatus.Success, result.Status);
        Assert.Equal(expected, Assert.Single(result.PendingExecutions!).Status);
        Assert.NotNull(result.PendingExecutions![0].Confirmation);
    }

    [Fact]
    public async Task MissingCurrentPendingIsUnavailableRatherThanClaimedExecutedOrExpired()
    {
        var correlation = new ConversationExecutionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var service = new ConversationPendingExecutionQueryService(new CorrelationStore(correlation), new ConfirmationService(correlation.PendingExecutionId, null));

        var result = await service.ListByConversationAsync(new(correlation.ConversationId));

        Assert.Equal(ConversationPendingExecutionStatus.Unavailable, Assert.Single(result.PendingExecutions!).Status);
    }

    private sealed class CorrelationStore(ConversationExecutionCorrelation correlation) : IConversationExecutionCorrelationStore
    {
        public Task<ConversationExecutionCorrelationRegisterResult> RegisterAsync(RegisterConversationExecutionCorrelationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationExecutionCorrelationRegisterResult(ConversationExecutionCorrelationStatus.Failed));
        public Task<ConversationExecutionCorrelationGetResult> GetByPendingExecutionIdAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationExecutionCorrelationGetResult(ConversationExecutionCorrelationStatus.NotFound));
        public Task<ConversationExecutionCorrelationListResult> ListByConversationAsync(ConversationExecutionCorrelationListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(query.ConversationId == correlation.ConversationId ? new ConversationExecutionCorrelationListResult(ConversationExecutionCorrelationStatus.Success, [correlation]) : new ConversationExecutionCorrelationListResult(ConversationExecutionCorrelationStatus.NotFound));
    }

    private sealed class ConfirmationService(Guid pendingExecutionId, PendingExecutionStatus? status) : IExecutionConfirmationService
    {
        public Task<ExecutionConfirmationResult> CreatePendingAsync(Plan plan, Guid taskId, CancellationToken cancellationToken = default) => Task.FromResult(new ExecutionConfirmationResult(PendingExecutionStatus.NotConfirmable));
        public Task<ExecutionConfirmationResult?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == pendingExecutionId && status is not null ? new ExecutionConfirmationResult(status.Value, new ExecutionConfirmationRequest(id, Guid.NewGuid(), Guid.NewGuid(), "tool", "Description", ExecutionRiskLevel.High, ExecutionPolicyReason.SideEffectRequiresConfirmation, "safe", DateTimeOffset.UtcNow.AddMinutes(1))) : null);
        public Task<ExecutionConfirmationResult?> DecideAsync(Guid pendingExecutionId, ExecutionConfirmationDecision decision, CancellationToken cancellationToken = default) => Task.FromResult<ExecutionConfirmationResult?>(null);
        public Task<PendingExecution?> TryTakeApprovedExecutionAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default) => Task.FromResult<PendingExecution?>(null);
    }
}
