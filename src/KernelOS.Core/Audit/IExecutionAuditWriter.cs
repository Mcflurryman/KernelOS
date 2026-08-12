namespace KernelOS.Core.Audit;

public interface IExecutionAuditWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
