namespace KernelOS.Infrastructure;

public interface IOllamaHealthCheck
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
