namespace KernelOS.Core.Execution;

public sealed record ExecutionPolicyRequest(Guid PlanId, Guid TaskId, string ToolName, ToolExecutionMetadata? ToolMetadata);
