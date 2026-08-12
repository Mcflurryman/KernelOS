using KernelOS.Core.Audit;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Execution;

public sealed class InMemoryExecutionAuditTrail(
    IOptions<ExecutionAuditOptions> options) : IExecutionAuditTrail
{
    private readonly object sync = new();
    private readonly Queue<AuditEvent> events = new();
    private readonly int maxEvents = options.Value.MaxEvents > 0
        ? options.Value.MaxEvents
        : throw new ArgumentOutOfRangeException(nameof(options), "ExecutionAudit:MaxEvents must be greater than zero.");

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(auditEvent);

        lock (sync)
        {
            events.Enqueue(auditEvent);
            while (events.Count > maxEvents)
            {
                events.Dequeue();
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>(events.ToArray());
        }
    }
}
