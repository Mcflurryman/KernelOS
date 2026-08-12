using Microsoft.Data.Sqlite;

namespace KernelOS.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(PersistencePathResolver paths) : ISqliteConnectionFactory
{
    private readonly string connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = paths.DatabasePath,
        Pooling = true,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout = 5000;", cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ExecutePragmaAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
