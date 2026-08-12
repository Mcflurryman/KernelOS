using KernelOS.Core.Audit;
using Microsoft.Extensions.Logging;

namespace KernelOS.Infrastructure.Execution;

public sealed class SafeExecutionAuditWriter(IExecutionAuditTrail trail, ILogger<SafeExecutionAuditWriter> logger) : IExecutionAuditWriter
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try { await trail.WriteAsync(auditEvent, cancellationToken); }
        // Audit sinks are observational only. Do not attach their exception to the log:
        // a third-party sink can include arbitrary event data in its exception message.
        catch (Exception) { SafeExecutionAuditWriterLog.WriteFailed(logger); }
    }
}

internal static partial class SafeExecutionAuditWriterLog
{
    [LoggerMessage(EventId = 30, Level = LogLevel.Warning, Message = "Execution audit event could not be recorded.")]
    internal static partial void WriteFailed(ILogger logger);
}
