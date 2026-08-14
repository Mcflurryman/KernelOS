using KernelOS.Core.Conversation;
using KernelOS.Infrastructure.Conversation;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace KernelOS.Tests;

public sealed class SqliteConversationStoreTests
{
    [Fact]
    public async Task VersionOneDatabaseMigratesToCurrentVersionWithoutChangingMemoryData()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        await fixture.ExecuteAsync("CREATE TABLE memory_documents (id TEXT PRIMARY KEY NOT NULL, content TEXT NOT NULL); INSERT INTO memory_documents VALUES ('memory-1', 'intact'); CREATE TABLE schema_version (singleton INTEGER PRIMARY KEY CHECK (singleton = 1), version INTEGER NOT NULL); INSERT INTO schema_version VALUES (1, 1);");

        await fixture.Initializer.InitializeAsync();

        Assert.Equal("intact", await fixture.ScalarAsync("SELECT content FROM memory_documents WHERE id = 'memory-1';"));
        Assert.Equal(3L, Convert.ToInt64(await fixture.ScalarAsync("SELECT version FROM schema_version WHERE singleton = 1;"), CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'conversation_messages';"), CultureInfo.InvariantCulture));
        await fixture.Initializer.InitializeAsync();
    }

    [Fact]
    public async Task FailedVersionTwoMigrationRollsBackAndLeavesVersionOne()
    {
        await using var fixture = await MigrationFixture.CreateAsync();
        await fixture.ExecuteAsync("CREATE TABLE schema_version (singleton INTEGER PRIMARY KEY CHECK (singleton = 1), version INTEGER NOT NULL); INSERT INTO schema_version VALUES (1, 1); CREATE TABLE conversation_messages (id TEXT PRIMARY KEY); ");

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync());

        Assert.Equal(1L, Convert.ToInt64(await fixture.ScalarAsync("SELECT version FROM schema_version WHERE singleton = 1;"), CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'conversations';"), CultureInfo.InvariantCulture));
    }
    [Fact]
    public async Task CreateAppendGetListAndMessagesAreDurableAndOrdered()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var created = await fixture.Store.CreateAsync();
        var conversation = created.Conversation!;
        var first = await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "hello"));
        var second = await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.Assistant, "world"));
        var restarted = fixture.NewStore();

        var loaded = await restarted.GetAsync(conversation.Id);
        var messages = await restarted.GetMessagesAsync(new(conversation.Id, 10));
        var listed = await restarted.ListAsync(new(10));

        Assert.Equal(ConversationStatus.Success, created.Status);
        Assert.Equal(1, first.Message!.Sequence); Assert.Equal(2, second.Message!.Sequence);
        Assert.Equal(3, loaded.Conversation!.Version);
        Assert.Equal(["hello", "world"], messages.Messages!.Select(x => x.Content));
        Assert.Equal([conversation.Id], listed.Conversations!.Select(x => x.Id));
    }

    [Fact]
    public async Task ListAndMessagePagingApplyLimitsWithoutLoadingOtherRows()
    {
        var clock = new TestTimeProvider();
        await using var fixture = await ConversationFixture.CreateAsync(clock: clock);
        var first = (await fixture.Store.CreateAsync()).Conversation!;
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = (await fixture.Store.CreateAsync()).Conversation!;
        foreach (var content in new[] { "one", "two", "three" }) Assert.Equal(ConversationStatus.Success, (await fixture.Store.AppendMessageAsync(new(first.Id, ConversationRole.User, content))).Status);
        clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.Store.AppendMessageAsync(new(second.Id, ConversationRole.User, "newer"));

        Assert.Equal([second.Id], (await fixture.Store.ListAsync(new(1, 0))).Conversations!.Select(x => x.Id));
        Assert.Equal(["two"], (await fixture.Store.GetMessagesAsync(new(first.Id, 1, 1))).Messages!.Select(x => x.Content));
    }

    [Fact]
    public async Task BeforeSequenceReturnsTheLatestBoundedWindowInAscendingOrder()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var conversation = (await fixture.Store.CreateAsync()).Conversation!;
        foreach (var content in new[] { "one", "two", "three", "four", "five" })
            await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, content));

        var bounded = await fixture.Store.GetMessagesAsync(new(conversation.Id, 2, BeforeSequence: 5));
        var empty = await fixture.Store.GetMessagesAsync(new(conversation.Id, 2, BeforeSequence: 1));

        Assert.Equal(["three", "four"], bounded.Messages!.Select(message => message.Content));
        Assert.Empty(empty.Messages!);
    }

    [Fact]
    public async Task ConcurrentAppendsHaveExactSequencesAndVersion()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var conversation = (await fixture.Store.CreateAsync()).Conversation!;
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(index => fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, $"message-{index}"))));
        var messages = await fixture.Store.GetMessagesAsync(new(conversation.Id, 20));

        Assert.All(results, result => Assert.Equal(ConversationStatus.Success, result.Status));
        Assert.Equal(Enumerable.Range(1, 16).Select(x => (long)x), messages.Messages!.Select(x => x.Sequence));
        Assert.Equal(17, (await fixture.Store.GetAsync(conversation.Id)).Conversation!.Version);
    }

    [Fact]
    public async Task DeleteCascadesMessagesAndMissingOrInvalidRequestsAreMapped()
    {
        await using var fixture = await ConversationFixture.CreateAsync(maxCharacters: 3);
        var conversation = (await fixture.Store.CreateAsync()).Conversation!;
        await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "ok"));

        Assert.Equal(ConversationStatus.InvalidRequest, (await fixture.Store.AppendMessageAsync(new(conversation.Id, (ConversationRole)99, "ok"))).Status);
        Assert.Equal(ConversationStatus.InvalidRequest, (await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "   "))).Status);
        Assert.Equal(ConversationStatus.InvalidRequest, (await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "four"))).Status);
        Assert.Equal(ConversationStatus.NotFound, (await fixture.Store.AppendMessageAsync(new(Guid.NewGuid(), ConversationRole.User, "ok"))).Status);
        Assert.Equal(ConversationStatus.Success, (await fixture.Store.DeleteAsync(conversation.Id)).Status);
        Assert.Equal(ConversationStatus.NotFound, (await fixture.Store.GetMessagesAsync(new(conversation.Id, 10))).Status);
        Assert.Equal(ConversationStatus.NotFound, (await fixture.Store.DeleteAsync(conversation.Id)).Status);
        Assert.Equal(ConversationStatus.InvalidRequest, (await fixture.Store.GetAsync(Guid.Empty)).Status);
    }

    [Fact]
    public async Task InjectionContentIsStoredAsDataAndCancelledOperationsDoNotWrite()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var conversation = (await fixture.Store.CreateAsync()).Conversation!;
        const string content = "'); DROP TABLE conversations; --";
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();

        Assert.Equal(ConversationStatus.Cancelled, (await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, "no"), cancelled.Token)).Status);
        Assert.Equal(ConversationStatus.Success, (await fixture.Store.AppendMessageAsync(new(conversation.Id, ConversationRole.User, content))).Status);
        Assert.Equal(content, (await fixture.Store.GetMessagesAsync(new(conversation.Id, 10))).Messages!.Single().Content);
    }

    private sealed class ConversationFixture : IAsyncDisposable
    {
        private readonly SqliteMemoryFixture persistence;
        private readonly int maxCharacters;
        private readonly TimeProvider timeProvider;
        private ConversationFixture(SqliteMemoryFixture persistence, int maxCharacters, TimeProvider timeProvider) { this.persistence = persistence; this.maxCharacters = maxCharacters; this.timeProvider = timeProvider; Store = NewStore(); }
        internal SqliteConversationStore Store { get; }
        internal static async Task<ConversationFixture> CreateAsync(int maxCharacters = 16_000, TimeProvider? clock = null) => new(await SqliteMemoryFixture.CreateAsync(), maxCharacters, clock ?? TimeProvider.System);
        internal SqliteConversationStore NewStore() => new(persistence.Factory, Options.Create(new ConversationMemoryOptions { MaxMessageCharacters = maxCharacters, MaxListPageSize = 10, MaxMessagesPageSize = 20 }), NullLogger<SqliteConversationStore>.Instance, timeProvider);
        public ValueTask DisposeAsync() => persistence.DisposeAsync();
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
