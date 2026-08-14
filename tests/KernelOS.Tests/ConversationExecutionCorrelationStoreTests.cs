using System.Globalization;
using KernelOS.Core.Conversation;
using KernelOS.Infrastructure.Conversation;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class ConversationExecutionCorrelationStoreTests
{
    [Fact]
    public async Task RegisterGetAndRestartPreserveOnlyCorrelationIdentity()
    {
        var clock = new TestTimeProvider();
        await using var fixture = await CorrelationFixture.CreateAsync(clock);
        var (conversation, user, assistant) = await fixture.CreateConversationWithMessagesAsync();
        var pendingId = Guid.NewGuid();

        var registered = await fixture.Store.RegisterAsync(new(pendingId, conversation.Id, user.Id, assistant.Id));
        var restarted = fixture.NewCorrelationStore();
        var loaded = await restarted.GetByPendingExecutionIdAsync(pendingId);

        Assert.Equal(ConversationExecutionCorrelationStatus.Success, registered.Status);
        Assert.Equal(clock.GetUtcNow(), registered.Correlation!.CreatedAt);
        Assert.Equal(ConversationExecutionCorrelationStatus.Success, loaded.Status);
        Assert.Equal(registered.Correlation, loaded.Correlation);
    }

    [Fact]
    public async Task RegisterSupportsNullAssistantAndIsIdempotentButDoesNotOverwriteConflicts()
    {
        await using var fixture = await CorrelationFixture.CreateAsync();
        var (firstConversation, firstUser, _) = await fixture.CreateConversationWithMessagesAsync();
        var (secondConversation, secondUser, _) = await fixture.CreateConversationWithMessagesAsync();
        var pendingId = Guid.NewGuid();

        var first = await fixture.Store.RegisterAsync(new(pendingId, firstConversation.Id, firstUser.Id));
        var duplicate = await fixture.Store.RegisterAsync(new(pendingId, firstConversation.Id, firstUser.Id));
        var conflict = await fixture.Store.RegisterAsync(new(pendingId, secondConversation.Id, secondUser.Id));
        var loaded = await fixture.Store.GetByPendingExecutionIdAsync(pendingId);

        Assert.Equal(ConversationExecutionCorrelationStatus.Success, first.Status);
        Assert.Null(first.Correlation!.AssistantMessageId);
        Assert.Equal(ConversationExecutionCorrelationStatus.Success, duplicate.Status);
        Assert.Equal(first.Correlation, duplicate.Correlation);
        Assert.Equal(ConversationExecutionCorrelationStatus.Conflict, conflict.Status);
        Assert.Equal(first.Correlation, loaded.Correlation);
    }

    [Fact]
    public async Task RegisterValidatesIdsAndMessageOwnership()
    {
        await using var fixture = await CorrelationFixture.CreateAsync();
        var (firstConversation, firstUser, firstAssistant) = await fixture.CreateConversationWithMessagesAsync();
        var (secondConversation, secondUser, secondAssistant) = await fixture.CreateConversationWithMessagesAsync();

        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.RegisterAsync(new(Guid.Empty, firstConversation.Id, firstUser.Id))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), Guid.Empty, firstUser.Id))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, Guid.Empty))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, firstUser.Id, Guid.Empty))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), Guid.NewGuid(), firstUser.Id))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, Guid.NewGuid()))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, secondUser.Id))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, firstUser.Id, Guid.NewGuid()))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), firstConversation.Id, firstUser.Id, secondAssistant.Id))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.Success, (await fixture.Store.RegisterAsync(new(Guid.NewGuid(), secondConversation.Id, secondUser.Id, secondAssistant.Id))).Status);
        Assert.NotEqual(firstAssistant.ConversationId, secondAssistant.ConversationId);
    }

    [Fact]
    public async Task GetListPagingAndConversationIsolationAreDeterministic()
    {
        var clock = new TestTimeProvider();
        await using var fixture = await CorrelationFixture.CreateAsync(clock, maxListPageSize: 2);
        var (first, firstUser, _) = await fixture.CreateConversationWithMessagesAsync();
        var (second, secondUser, _) = await fixture.CreateConversationWithMessagesAsync();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
        {
            await fixture.Store.RegisterAsync(new(id, first.Id, firstUser.Id));
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        await fixture.Store.RegisterAsync(new(Guid.NewGuid(), second.Id, secondUser.Id));

        var firstPage = await fixture.Store.ListByConversationAsync(new(first.Id, 20));
        var secondPage = await fixture.Store.ListByConversationAsync(new(first.Id, 1, 1));
        var isolated = await fixture.Store.ListByConversationAsync(new(second.Id, 10));

        Assert.Equal(ConversationExecutionCorrelationStatus.Success, firstPage.Status);
        Assert.Equal([ids[2], ids[1]], firstPage.Correlations!.Select(value => value.PendingExecutionId));
        Assert.Equal([ids[1]], secondPage.Correlations!.Select(value => value.PendingExecutionId));
        Assert.Single(isolated.Correlations!);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.GetByPendingExecutionIdAsync(Guid.NewGuid())).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.GetByPendingExecutionIdAsync(Guid.Empty)).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.ListByConversationAsync(new(Guid.NewGuid()))).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.InvalidRequest, (await fixture.Store.ListByConversationAsync(new(first.Id, 0))).Status);
    }

    [Fact]
    public async Task ConversationDeleteCascadesCorrelationAndDatabaseIntegrityRemainsClean()
    {
        await using var fixture = await CorrelationFixture.CreateAsync();
        var (conversation, user, _) = await fixture.CreateConversationWithMessagesAsync();
        var pendingId = Guid.NewGuid();
        await fixture.Store.RegisterAsync(new(pendingId, conversation.Id, user.Id));

        Assert.Equal(ConversationStatus.Success, (await fixture.Conversations.DeleteAsync(conversation.Id)).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.GetByPendingExecutionIdAsync(pendingId)).Status);
        Assert.Equal("ok", await fixture.ScalarAsync("PRAGMA integrity_check;"));
        Assert.Null(await fixture.ScalarAsync("PRAGMA foreign_key_check;"));
    }

    [Fact]
    public async Task CancelledOperationsDoNotWrite()
    {
        await using var fixture = await CorrelationFixture.CreateAsync();
        var (conversation, user, _) = await fixture.CreateConversationWithMessagesAsync();
        var pendingId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Equal(ConversationExecutionCorrelationStatus.Cancelled, (await fixture.Store.RegisterAsync(new(pendingId, conversation.Id, user.Id), cancellation.Token)).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.Cancelled, (await fixture.Store.GetByPendingExecutionIdAsync(pendingId, cancellation.Token)).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.Cancelled, (await fixture.Store.ListByConversationAsync(new(conversation.Id), cancellation.Token)).Status);
        Assert.Equal(ConversationExecutionCorrelationStatus.NotFound, (await fixture.Store.GetByPendingExecutionIdAsync(pendingId)).Status);
    }

    [Fact]
    public async Task VersionTwoDatabaseMigratesToVersionThreeWithoutChangingExistingDataAndStartupIsIdempotent()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        await fixture.ExecuteAsync(VersionTwoSchemaSql);

        await fixture.Initializer.InitializeAsync();
        await fixture.Initializer.InitializeAsync();

        Assert.Equal("intact", await fixture.ScalarAsync("SELECT content FROM memory_documents WHERE id = 'memory-1';"));
        Assert.Equal("user", await fixture.ScalarAsync("SELECT content FROM conversation_messages WHERE id = '22222222-2222-2222-2222-222222222222';"));
        Assert.Equal(3L, Convert.ToInt64(await fixture.ScalarAsync("SELECT version FROM schema_version WHERE singleton = 1;"), CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'conversation_pending_executions';"), CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_conversation_pending_executions_conversation_created';"), CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task FailedVersionThreeMigrationRollsBackCompletely()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        await fixture.ExecuteAsync(VersionTwoSchemaSql + " CREATE TABLE migration_collision (id TEXT PRIMARY KEY); CREATE INDEX ix_conversation_pending_executions_conversation_created ON migration_collision(id);");

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync());

        Assert.Equal(2L, Convert.ToInt64(await fixture.ScalarAsync("SELECT version FROM schema_version WHERE singleton = 1;"), CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'conversation_pending_executions';"), CultureInfo.InvariantCulture));
        Assert.Equal("intact", await fixture.ScalarAsync("SELECT content FROM memory_documents WHERE id = 'memory-1';"));
        Assert.Equal("user", await fixture.ScalarAsync("SELECT content FROM conversation_messages WHERE id = '22222222-2222-2222-2222-222222222222';"));
    }

    [Fact]
    public async Task FutureSchemaFailsSafely()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        await fixture.ExecuteAsync(VersionTwoSchemaSql);
        await fixture.Initializer.InitializeAsync();
        await fixture.ExecuteAsync("UPDATE schema_version SET version = 4 WHERE singleton = 1;");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Initializer.InitializeAsync());
    }

    private const string VersionTwoSchemaSql = """
        CREATE TABLE memory_documents (id TEXT PRIMARY KEY NOT NULL, content TEXT NOT NULL);
        INSERT INTO memory_documents VALUES ('memory-1', 'intact');
        CREATE TABLE schema_version (singleton INTEGER PRIMARY KEY CHECK (singleton = 1), version INTEGER NOT NULL);
        INSERT INTO schema_version VALUES (1, 2);
        CREATE TABLE conversations (id TEXT PRIMARY KEY NOT NULL, created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL, version_number INTEGER NOT NULL CHECK (version_number > 0));
        INSERT INTO conversations VALUES ('11111111-1111-1111-1111-111111111111', '2026-01-01T00:00:00.0000000+00:00', '2026-01-01T00:00:00.0000000+00:00', 1);
        CREATE TABLE conversation_messages (id TEXT PRIMARY KEY NOT NULL, conversation_id TEXT NOT NULL, sequence_number INTEGER NOT NULL CHECK (sequence_number > 0), role INTEGER NOT NULL CHECK (role IN (0, 1)), content TEXT NOT NULL, created_at_utc TEXT NOT NULL, UNIQUE (conversation_id, sequence_number), FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE);
        INSERT INTO conversation_messages VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 1, 0, 'user', '2026-01-01T00:00:00.0000000+00:00');
        """;

    private sealed class CorrelationFixture : IAsyncDisposable
    {
        private readonly string directory;
        private readonly SqliteConnectionFactory factory;
        private readonly ConversationMemoryOptions options;
        private readonly TimeProvider clock;

        private CorrelationFixture(string directory, SqliteConnectionFactory factory, ConversationMemoryOptions options, TimeProvider clock)
        {
            this.directory = directory;
            this.factory = factory;
            this.options = options;
            this.clock = clock;
            Conversations = NewConversationStore();
            Store = NewCorrelationStore();
        }

        internal SqliteConversationStore Conversations { get; }
        internal SqliteConversationExecutionCorrelationStore Store { get; }

        internal static async Task<CorrelationFixture> CreateAsync(TimeProvider? clock = null, int maxListPageSize = 10)
        {
            var directory = Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
            var paths = new PersistencePathResolver(Options.Create(new PersistenceOptions { DataDirectory = directory, DatabaseFile = "correlation.db" }));
            var factory = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, factory).InitializeAsync();
            return new(directory, factory, new ConversationMemoryOptions { MaxMessageCharacters = 16_000, MaxListPageSize = maxListPageSize, MaxMessagesPageSize = 50 }, clock ?? TimeProvider.System);
        }

        internal SqliteConversationExecutionCorrelationStore NewCorrelationStore() => new(factory, Options.Create(options), NullLogger<SqliteConversationExecutionCorrelationStore>.Instance, clock);
        private SqliteConversationStore NewConversationStore() => new(factory, Options.Create(options), NullLogger<SqliteConversationStore>.Instance, clock);
        internal async Task<(Conversation Conversation, ConversationMessage User, ConversationMessage Assistant)> CreateConversationWithMessagesAsync()
        {
            var conversation = (await Conversations.CreateAsync()).Conversation!;
            var user = (await Conversations.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "user"))).Message!;
            var assistant = (await Conversations.AppendMessageAsync(new(conversation.Id, ConversationRole.Assistant, "assistant"))).Message!;
            return (conversation, user, assistant);
        }
        internal async Task<object?> ScalarAsync(string sql) { await using var connection = await factory.OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }
        public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); return ValueTask.CompletedTask; }
    }

    private sealed class MigrationFixture : IAsyncDisposable
    {
        private readonly string directory;
        private readonly SqliteConnectionFactory factory;
        private MigrationFixture(string directory, SqliteConnectionFactory factory, SqliteDatabaseInitializer initializer) { this.directory = directory; this.factory = factory; Initializer = initializer; }
        internal SqliteDatabaseInitializer Initializer { get; }
        internal static Task<MigrationFixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
            var paths = new PersistencePathResolver(Options.Create(new PersistenceOptions { DataDirectory = directory, DatabaseFile = "migration.db" }));
            var factory = new SqliteConnectionFactory(paths);
            return Task.FromResult(new MigrationFixture(directory, factory, new SqliteDatabaseInitializer(paths, factory)));
        }
        internal async Task ExecuteAsync(string sql) { Directory.CreateDirectory(directory); await using var connection = await factory.OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
        internal async Task<object?> ScalarAsync(string sql) { await using var connection = await factory.OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }
        public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); return ValueTask.CompletedTask; }
    }
}
