using Microsoft.Extensions.Hosting;

namespace KernelOS.Infrastructure.Persistence;

internal sealed class SqliteDatabaseInitializationHostedService(ISqliteDatabaseInitializer initializer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => initializer.InitializeAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
