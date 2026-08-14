using System.Reflection;
using Microsoft.Data.Sqlite;

namespace KernelOS.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer(PersistencePathResolver paths, ISqliteConnectionFactory connections) : ISqliteDatabaseInitializer
{
    private const int CurrentVersion = 3;
    private static readonly IReadOnlyList<string> MigrationResources =
    [
        "KernelOS.Infrastructure.Persistence.Migrations.001_initial.sql",
        "KernelOS.Infrastructure.Persistence.Migrations.002_conversation_memory.sql",
        "KernelOS.Infrastructure.Persistence.Migrations.003_conversation_pending_correlation.sql"
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA synchronous = FULL;", cancellationToken);
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS schema_version (singleton INTEGER PRIMARY KEY CHECK (singleton = 1), version INTEGER NOT NULL); INSERT OR IGNORE INTO schema_version (singleton, version) VALUES (1, 0);", cancellationToken);
        var version = await CurrentVersionAsync(connection, cancellationToken);
        if (version > CurrentVersion) throw new InvalidOperationException("The persistence schema is newer than this application.");
        while (version < CurrentVersion)
        {
            var nextVersion = version + 1;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken) as SqliteTransaction
                ?? throw new InvalidOperationException("SQLite could not start a migration transaction.");
            try
            {
            await ExecuteAsync(connection, ReadMigration(nextVersion), cancellationToken, transaction);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE schema_version SET version = $version WHERE singleton = 1;";
            command.Parameters.AddWithValue("$version", nextVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            version = nextVersion;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private static async Task<int> CurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version WHERE singleton = 1;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ReadMigration(int version)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MigrationResources[version - 1]) ?? throw new InvalidOperationException("The persistence migration is unavailable.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
