using System.Text.Json;
using KernelOS.Core.Conversation;
using KernelOS.Core.Execution;
using KernelOS.Core.Rag;
using KernelOS.Core.Planning;
namespace KernelOS.Core.Kai;
public enum KaiMode { Auto, Chat, Rag, Planner }
public enum KaiStatus { Success, PartialSuccess, NoContext, InvalidRequest, ProviderUnavailable, Cancelled, Failed, PlanningFailed, RequiresConfirmation, Denied, Completed }
public sealed record KaiRequest(string? Message, IReadOnlyList<ConversationTurn>? History = null, KaiMode PreferredMode = KaiMode.Auto, string? ToolName = null, IReadOnlyDictionary<string, JsonElement>? Arguments = null);
public sealed record KaiDecision(KaiMode Mode, string ReasonCode);
public sealed record KaiWarning(string Code,string Message);
public sealed record KaiResponse(KaiStatus Status,KaiMode ModeUsed,string Answer="",IReadOnlyList<RagCitation>? Citations=null,IReadOnlyList<KaiWarning>? Warnings=null,string? Model=null,KaiDecision? Decision=null,Guid? PlanId=null,Guid? PendingExecutionId=null,ExecutionConfirmationRequest? Confirmation=null,PlannerResult? Execution=null);
public sealed record KaiOptionsSnapshot(int MaxMessageCharacters,KaiMode DefaultMode,bool AllowAutoRagFallbackToChat,bool AllowPlannerInAuto);
public interface IKaiAgent { Task<KaiResponse> HandleAsync(KaiRequest request,CancellationToken cancellationToken=default); }
public interface IKaiIntentRouter { KaiDecision Route(string message,KaiMode preferredMode); }
