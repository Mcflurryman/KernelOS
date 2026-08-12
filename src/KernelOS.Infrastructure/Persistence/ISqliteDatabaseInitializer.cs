namespace KernelOS.Infrastructure.Persistence;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
