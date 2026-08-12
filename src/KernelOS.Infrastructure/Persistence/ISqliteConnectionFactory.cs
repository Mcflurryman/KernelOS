using Microsoft.Data.Sqlite;

namespace KernelOS.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
