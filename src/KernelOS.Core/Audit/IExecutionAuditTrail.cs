namespace KernelOS.Core.Audit;

public interface IExecutionAuditTrail
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
