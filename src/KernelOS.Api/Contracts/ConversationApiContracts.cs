using System.Text.Json;
using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;
using KernelOS.Core.Kai;
using KernelOS.Core.Planning;

namespace KernelOS.Api.Contracts;

public sealed record ConversationTurnApiRequest(string? Message, KaiMode PreferredMode = KaiMode.Auto, string? ToolName = null, IReadOnlyDictionary<string, JsonElement>? Arguments = null);
public sealed record ConversationApiResponse(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version);
public sealed record ConversationMessageApiResponse(Guid Id, Guid ConversationId, long Sequence, string Role, string Content, DateTimeOffset CreatedAt);
public sealed record ConversationTurnApiResponse(Guid ConversationId, Guid? UserMessageId, Guid? AssistantMessageId, string TurnStatus, string? KaiStatus, string? ModeUsed, string? Answer, object? Citations, object? Warnings, string? Model, Guid? PendingExecutionId, object? Confirmation, string? ErrorCode);
public sealed record ExecutionConfirmationApiResponse(PendingExecutionStatus Status, ExecutionConfirmationPublicApiResponse? Confirmation, bool Transitioned);
public sealed record ExecutionConfirmationPublicApiResponse(Guid PendingExecutionId, string Description, ExecutionRiskLevel RiskLevel, ExecutionPolicyReason Reason, string SafeArgumentSummary, DateTimeOffset ExpiresAt, int TaskCount);
public sealed record ConversationPendingExecutionApiResponse(Guid PendingExecutionId, Guid UserMessageId, Guid? AssistantMessageId, DateTimeOffset CreatedAt, ConversationPendingExecutionStatus Status, ExecutionConfirmationPublicApiResponse? Confirmation);
public sealed record ExecutionApiResponse(PlannerStatus Status, int CompletedTaskCount, int TotalTaskCount, string? ErrorCode);
