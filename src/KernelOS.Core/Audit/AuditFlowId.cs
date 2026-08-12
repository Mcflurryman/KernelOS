namespace KernelOS.Core.Audit;

public readonly record struct AuditFlowId(Guid Value)
{
    public static AuditFlowId Create() => new(Guid.NewGuid());
}
