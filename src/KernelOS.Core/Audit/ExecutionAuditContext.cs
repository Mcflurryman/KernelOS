namespace KernelOS.Core.Audit;

public sealed record ExecutionAuditContext(AuditFlowId FlowId, ExecutionOrigin Origin);
