using System.Globalization;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Memory;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class SqliteMemoryStoreTests
{
    [Fact]
    public async Task InitializerCreatesDatabaseSchemaAndIsIdempotent()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        await fixture.Initializer.InitializeAsync();
        await using var connection = await fixture.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version WHERE singleton = 1;";

        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        Assert.True(File.Exists(fixture.DatabasePath));
        Assert.True(Directory.Exists(fixture.Directory));
    }

    [Fact]
    public async Task InitializerRejectsNewerSchemaVersions()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        await using (var connection = await fixture.Factory.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE schema_version SET version = 2 WHERE singleton = 1;";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Initializer.InitializeAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../kernelos.db")]
    [InlineData("..\\kernelos.db")]
    [InlineData("nested/kernelos.db")]
    [InlineData("nested\\kernelos.db")]
    [InlineData("/absolute/kernelos.db")]
    [InlineData("C:\\temp\\kernelos.db")]
    [InlineData("C:kernelos.db")]
    [InlineData(":")]
    [InlineData(".")]
    [InlineData("..")]
    public void PathResolverRejectsUnsafeDatabaseFileNames(string databaseFile)
    {
        Assert.Throws<OptionsValidationException>(() => new PersistencePathResolver(Options.Create(new PersistenceOptions { DatabaseFile = databaseFile })));
    }

    [Theory]
    [InlineData("kernelos.db")]
    [InlineData("memory.sqlite")]
    [InlineData("kernel-os.db")]
    [InlineData("kernel_os.db")]
    public void PathResolverAcceptsSimpleDatabaseFileNames(string databaseFile)
    {
        var resolver = new PersistencePathResolver(Options.Create(new PersistenceOptions { DataDirectory = Path.GetTempPath(), DatabaseFile = databaseFile }));

        Assert.Equal(databaseFile, Path.GetFileName(resolver.DatabasePath));
    }

    [Fact]
    public async Task StoreAndGetPreserveTheCompleteAggregate()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var document = Document("first", "second");

        var stored = await fixture.Store.StoreAsync(new(document));
        var loaded = await fixture.Store.GetAsync(stored.Document!.Id.ToString("D"));

        Assert.Equal(MemoryStatus.Success, stored.Status);
        Assert.Equal(MemoryStatus.Success, loaded.Status);
        Assert.Equal(document.Id, loaded.Document!.Id);
        Assert.Equal(document.Id, loaded.Document.KnowledgeDocumentId);
        Assert.Equal(1, loaded.Document.Version.Number);
        var loadedDocument = Assert.IsType<MemoryDocument>(loaded.Document);
        Assert.Equal(document.Items.Select(item => item.Content), loadedDocument.Items.Select(item => item.Content));
        Assert.Equal("Kai", loadedDocument.Metadata.Properties!["author"]);
        Assert.Equal("en", loadedDocument.Items[1].Metadata.Language);
        Assert.Equal(2, loadedDocument.Items[1].Source.Locator!.Line);
        Assert.Equal("$.items[1]", loadedDocument.Items[1].Source.Locator!.JsonPath);
    }

    [Fact]
    public async Task StoreRejectsDuplicateAndHonorsLimitsAndCancellation()
    {
        await using var duplicateFixture = await SqliteMemoryFixture.CreateAsync(maxDocuments: 2, maxItems: 1);
        var document = Document("one");
        Assert.Equal(MemoryStatus.Success, (await duplicateFixture.Store.StoreAsync(new(document))).Status);
        Assert.Equal(MemoryStatus.AlreadyExists, (await duplicateFixture.Store.StoreAsync(new(document))).Status);
        await using var limitedFixture = await SqliteMemoryFixture.CreateAsync(maxDocuments: 1, maxItems: 1);
        Assert.Equal(MemoryStatus.InvalidRequest, (await limitedFixture.Store.StoreAsync(new(Document("a", "b")))).Status);
        Assert.Equal(MemoryStatus.Success, (await limitedFixture.Store.StoreAsync(new(Document("one")))).Status);
        Assert.Equal(MemoryStatus.InvalidRequest, (await limitedFixture.Store.StoreAsync(new(Document("other")))).Status);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Equal(MemoryStatus.Cancelled, (await limitedFixture.Store.StoreAsync(new(Document("cancelled")), cancelled.Token)).Status);
    }

    [Fact]
    public async Task GetHandlesMissingAndInvalidIdentifiersAndReturnsSnapshots()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var stored = (await fixture.Store.StoreAsync(new(Document("one")))).Document!;
        var first = await fixture.Store.GetAsync(stored.Id.ToString("D"));
        var metadata = Assert.IsType<Dictionary<string, string>>(first.Document!.Metadata.Properties);
        metadata["author"] = "mutated";
        var second = await fixture.Store.GetAsync(stored.Id.ToString("D"));

        Assert.Equal(MemoryStatus.NotFound, (await fixture.Store.GetAsync("not-a-guid")).Status);
        Assert.Equal(MemoryStatus.NotFound, (await fixture.Store.GetAsync(Guid.NewGuid().ToString("D"))).Status);
        Assert.Equal("Kai", second.Document!.Metadata.Properties!["author"]);
    }

    [Fact]
    public async Task StoredDocumentSurvivesNewFactoryAndStoreInstances()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var document = Document("durable");
        var stored = await fixture.Store.StoreAsync(new(document));
        var options = Options.Create(new PersistenceOptions { DataDirectory = fixture.Directory, DatabaseFile = "memory.db" });
        var paths = new PersistencePathResolver(options);
        var factory = new SqliteConnectionFactory(paths);
        var store = new SqliteMemoryStore(factory, Options.Create(new MemoryOptions()), NullLogger<SqliteMemoryStore>.Instance);

        var loaded = await store.GetAsync(stored.Document!.Id.ToString("D"));

        Assert.Equal(MemoryStatus.Success, loaded.Status);
        Assert.Equal("durable", loaded.Document!.Items.Single().Content);
    }

    [Fact]
    public async Task ConcurrentStoresForSameKnowledgeDocumentProduceOneAggregate()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var document = Document("same");
        var results = await Task.WhenAll(fixture.Store.StoreAsync(new(document)), fixture.Store.StoreAsync(new(document)));

        Assert.Equal(1, results.Count(result => result.Status == MemoryStatus.Success));
        Assert.Equal(1, results.Count(result => result.Status == MemoryStatus.AlreadyExists));
        Assert.Equal(MemoryStatus.Success, (await fixture.Store.GetAsync(document.Id.ToString("D"))).Status);
    }

    [Fact]
    public async Task FailedItemInsertRollsBackTheCompleteAggregate()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        await using (var connection = await fixture.Factory.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER abort_rollback_item BEFORE INSERT ON memory_items WHEN NEW.content = 'rollback' BEGIN SELECT RAISE(ABORT, 'forced'); END;";
            await command.ExecuteNonQueryAsync();
        }

        var document = Document("rollback");
        var stored = await fixture.Store.StoreAsync(new(document));

        Assert.Equal(MemoryStatus.Failed, stored.Status);
        Assert.Equal(MemoryStatus.NotFound, (await fixture.Store.GetAsync(document.Id.ToString("D"))).Status);
    }

    [Fact]
    public async Task UpdateReplacesAggregateAndIncrementsVersion()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var created = (await fixture.Store.StoreAsync(new(Document("old")))).Document!;
        var metadata = new KnowledgeMetadata("text/markdown", "markdown", "en", new Dictionary<string, string> { ["author"] = "Updated" });
        var item = created.Items[0] with { Content = "new", ContentHash = "new-hash", Metadata = metadata };

        var updated = await fixture.Store.UpdateAsync(new(created.Id.ToString("D"), [item], metadata));
        var loaded = await fixture.Store.GetAsync(created.Id.ToString("D"));

        Assert.Equal(MemoryStatus.Success, updated.Status);
        Assert.Equal(2, updated.Document!.Version.Number);
        Assert.Equal(created.CreatedAt, updated.Document.CreatedAt);
        Assert.Equal("new", loaded.Document!.Items.Single().Content);
        Assert.Equal("Updated", loaded.Document.Metadata.Properties!["author"]);
    }

    [Fact]
    public async Task UpdateFailureRollsBackToThePreviousAggregate()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var created = (await fixture.Store.StoreAsync(new(Document("old")))).Document!;
        await using (var connection = await fixture.Factory.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER abort_update_item BEFORE INSERT ON memory_items WHEN NEW.content = 'rollback-update' BEGIN SELECT RAISE(ABORT, 'forced'); END;";
            await command.ExecuteNonQueryAsync();
        }

        var item = created.Items[0] with { Content = "rollback-update" };
        var result = await fixture.Store.UpdateAsync(new(created.Id.ToString("D"), [item], created.Metadata));
        var loaded = await fixture.Store.GetAsync(created.Id.ToString("D"));

        Assert.Equal(MemoryStatus.Failed, result.Status);
        Assert.Equal("old", loaded.Document!.Items.Single().Content);
        Assert.Equal(1, loaded.Document.Version.Number);
    }

    [Fact]
    public async Task DeleteAndQueryAreDurableAndMaintainIntegrity()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var first = (await fixture.Store.StoreAsync(new(Document("alpha")))).Document!;
        var second = (await fixture.Store.StoreAsync(new(Document("beta")))).Document!;
        var query = await fixture.Store.QueryAsync(new(ExactContent: "alpha", Limit: 10));
        var deleted = await fixture.Store.DeleteAsync(new(second.Id.ToString("D")));
        var options = Options.Create(new PersistenceOptions { DataDirectory = fixture.Directory, DatabaseFile = "memory.db" });
        var restarted = new SqliteMemoryStore(new SqliteConnectionFactory(new PersistencePathResolver(options)), Options.Create(new MemoryOptions()), NullLogger<SqliteMemoryStore>.Instance);

        Assert.Equal([first.Id], query.Documents!.Select(document => document.Id));
        Assert.Equal(MemoryStatus.Success, deleted.Status);
        Assert.Equal(MemoryStatus.Success, (await restarted.GetAsync(first.Id.ToString("D"))).Status);
        Assert.Equal(MemoryStatus.NotFound, (await restarted.GetAsync(second.Id.ToString("D"))).Status);
        await using var connection = await fixture.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task FullCrudSurvivesRestartWithVersionAndDatabaseIntegrity()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var first = (await fixture.Store.StoreAsync(new(Document("first")))).Document!;
        var second = (await fixture.Store.StoreAsync(new(Document("second")))).Document!;
        var metadata = new KnowledgeMetadata("text/plain", "text", "en", new Dictionary<string, string> { ["author"] = "Final" });
        var update1 = await fixture.Store.UpdateAsync(new(first.Id.ToString("D"), [first.Items[0] with { Content = "updated-1", ContentHash = "u1", Metadata = metadata }], metadata));
        var update2 = await fixture.Store.UpdateAsync(new(first.Id.ToString("D"), [update1.Document!.Items[0] with { Content = "updated-2", ContentHash = "u2" }], metadata));
        var deleted = await fixture.Store.DeleteAsync(new(second.Id.ToString("D")));
        Assert.Equal(MemoryStatus.Success, deleted.Status);
        var restarted = new SqliteMemoryStore(new SqliteConnectionFactory(new PersistencePathResolver(Options.Create(new PersistenceOptions { DataDirectory = fixture.Directory, DatabaseFile = "memory.db" }))), Options.Create(new MemoryOptions()), NullLogger<SqliteMemoryStore>.Instance);
        var loaded = await restarted.GetAsync(first.Id.ToString("D"));

        Assert.Equal(3, loaded.Document!.Version.Number);
        Assert.Equal(first.CreatedAt, loaded.Document.CreatedAt);
        Assert.Equal(update2.Document!.ContentHash, loaded.Document.ContentHash);
        Assert.Equal("updated-2", loaded.Document.Items.Single().Content);
        Assert.Equal("Final", loaded.Document.Metadata.Properties!["author"]);
        Assert.Equal(MemoryStatus.NotFound, (await restarted.GetAsync(second.Id.ToString("D"))).Status);
        Assert.Equal([first.Id], (await restarted.QueryAsync(new(Limit: 10))).Documents!.Select(document => document.Id));
        await using var connection = await fixture.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ConcurrentUpdatesProduceOneCompleteFinalAggregate()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var created = (await fixture.Store.StoreAsync(new(Document("old")))).Document!;
        var metadataA = Metadata("A");
        var metadataB = Metadata("B");
        var updateA = new MemoryUpdateRequest(created.Id.ToString("D"), [Item(created.Items[0], "item-A", metadataA)], metadataA);
        var updateB = new MemoryUpdateRequest(created.Id.ToString("D"), [Item(created.Items[0], "item-B", metadataB)], metadataB);
        using var start = new Barrier(2);

        async Task<MemoryUpdateResult> UpdateWhenReleasedAsync(MemoryUpdateRequest update)
        {
            start.SignalAndWait();
            return await fixture.Store.UpdateAsync(update);
        }

        var results = await Task.WhenAll(
            Task.Run(() => UpdateWhenReleasedAsync(updateA)),
            Task.Run(() => UpdateWhenReleasedAsync(updateB)));
        var final = await fixture.Store.GetAsync(created.Id.ToString("D"));

        Assert.All(results, result => Assert.Equal(MemoryStatus.Success, result.Status));
        Assert.Equal(3, final.Document!.Version.Number);
        Assert.True(IsSnapshot(final.Document, "A", "item-A") || IsSnapshot(final.Document, "B", "item-B"));
        await AssertDatabaseIntegrityAsync(fixture);
    }

    [Fact]
    public async Task GetAndQueryObserveOnlyCompleteSnapshotsDuringAnOpenUpdateTransaction()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync();
        var created = (await fixture.Store.StoreAsync(new(Document("item-OLD")))).Document!;
        await using var writer = await fixture.Factory.OpenConnectionAsync();
        await using var begin = writer.CreateCommand();
        begin.CommandText = "BEGIN IMMEDIATE;";
        await begin.ExecuteNonQueryAsync();

        await ExecuteAsync(writer, "DELETE FROM memory_document_metadata WHERE document_id = $id;", ("$id", created.Id.ToString("D")));
        await ExecuteAsync(writer, "INSERT INTO memory_document_metadata (document_id, key, value) VALUES ($id, 'author', 'NEW');", ("$id", created.Id.ToString("D")));

        var duringGet = fixture.Store.GetAsync(created.Id.ToString("D"));
        var duringQuery = fixture.Store.QueryAsync(new MemoryQuery(Limit: 10));
        var duringSnapshot = fixture.Store.CreateSnapshotAsync();
        await Task.WhenAll(duringGet, duringQuery, duringSnapshot);
        var observedGet = await duringGet;
        var observedQuery = await duringQuery;
        var observedSnapshot = await duringSnapshot;

        Assert.True(IsSnapshot(observedGet.Document!, "Kai", "item-OLD"));
        Assert.True(IsSnapshot(observedQuery.Documents!.Single(), "Kai", "item-OLD"));
        Assert.Equal(MemoryStatus.Success, observedSnapshot.Status);
        Assert.True(IsSnapshot(observedSnapshot.Snapshot!.Documents.Single(), "Kai", "item-OLD"));

        await ExecuteAsync(writer, "UPDATE memory_items SET content = $content, content_hash = $hash WHERE document_id = $id;", ("$content", "item-NEW"), ("$hash", "hash-item-NEW"), ("$id", created.Id.ToString("D")));
        await using var commit = writer.CreateCommand();
        commit.CommandText = "COMMIT;";
        await commit.ExecuteNonQueryAsync();

        Assert.True(IsSnapshot((await fixture.Store.GetAsync(created.Id.ToString("D"))).Document!, "NEW", "item-NEW"));
        Assert.True(IsSnapshot((await fixture.Store.QueryAsync(new MemoryQuery(Limit: 10))).Documents!.Single(), "NEW", "item-NEW"));
        await AssertDatabaseIntegrityAsync(fixture);
    }

    [Fact]
    public async Task QueryRemainsCorrectForOneHundredDocuments()
    {
        await using var fixture = await SqliteMemoryFixture.CreateAsync(maxDocuments: 120);
        var documents = Enumerable.Range(0, 100)
            .Select(index => DocumentWithAuthor($"content-{index:D3}", index % 10 == 0 ? "target" : "other"))
            .ToArray();
        foreach (var document in documents)
            Assert.Equal(MemoryStatus.Success, (await fixture.Store.StoreAsync(new(document))).Status);

        var expected = documents.Where((_, index) => index % 10 == 0).Reverse().Select(document => document.Id).ToArray();
        var page = await fixture.Store.QueryAsync(new MemoryQuery(MetadataKey: "author", MetadataValue: "target", Limit: 3, Offset: 2));

        Assert.Equal(MemoryStatus.Success, page.Status);
        Assert.Equal(expected.Skip(2).Take(3), page.Documents!.Select(document => document.Id));
        Assert.Equal([documents[42].Id], (await fixture.Store.QueryAsync(new MemoryQuery(ExactContent: "content-042", Limit: 10))).Documents!.Select(document => document.Id));
        Assert.Equal(MemoryStatus.Success, (await fixture.Store.GetAsync(documents[99].Id.ToString("D"))).Status);
        await AssertDatabaseIntegrityAsync(fixture);
    }

    private static KnowledgeMetadata Metadata(string author) => new("text/plain", "text", "es", new Dictionary<string, string> { ["author"] = author });

    private static MemoryItem Item(MemoryItem original, string content, KnowledgeMetadata metadata) => original with { Content = content, ContentHash = $"hash-{content}", Metadata = metadata };

    private static bool IsSnapshot(MemoryDocument document, string author, string content) =>
        document.Metadata.Properties!["author"] == author && document.Items.Count == 1 && document.Items[0].Content == content;

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertDatabaseIntegrityAsync(SqliteMemoryFixture fixture)
    {
        await using var connection = await fixture.Factory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    private static KnowledgeDocument Document(params string[] contents) => DocumentWithAuthor(contents, "Kai");

    private static KnowledgeDocument DocumentWithAuthor(string content, string author) => DocumentWithAuthor([content], author);

    private static KnowledgeDocument DocumentWithAuthor(string[] contents, string author)
    {
        var id = Guid.NewGuid();
        var metadata = new KnowledgeMetadata("text/plain", "text", "es", new Dictionary<string, string> { ["author"] = author });
        var items = contents.Select((content, index) => new KnowledgeItem(Guid.NewGuid(), index == 1 ? KnowledgeItemType.Code : KnowledgeItemType.Text, content, index, new KnowledgeSource(Guid.NewGuid(), $"safe-{index}", $"display-{index}", new(Line: index + 1, JsonPath: $"$.items[{index}]")), metadata with { Language = index == 1 ? "en" : "es" }, $"hash-{content}")).ToArray();
        return new(id, Guid.NewGuid(), "document", items, metadata, [], new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero), "document-hash");
    }
}

internal sealed class SqliteMemoryFixture : IAsyncDisposable
{
    private SqliteMemoryFixture(string directory, string databasePath, SqliteConnectionFactory factory, ISqliteDatabaseInitializer initializer, SqliteMemoryStore store)
    {
        Directory = directory; DatabasePath = databasePath; Factory = factory; Initializer = initializer; Store = store;
    }

    internal string Directory { get; }
    internal string DatabasePath { get; }
    internal SqliteConnectionFactory Factory { get; }
    internal ISqliteDatabaseInitializer Initializer { get; }
    internal SqliteMemoryStore Store { get; }

    internal static async Task<SqliteMemoryFixture> CreateAsync(int maxDocuments = 100, int maxItems = 100)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KernelOS.Tests", Guid.NewGuid().ToString("N"));
        var paths = new PersistencePathResolver(Options.Create(new PersistenceOptions { DataDirectory = directory, DatabaseFile = "memory.db" }));
        var factory = new SqliteConnectionFactory(paths);
        var initializer = new SqliteDatabaseInitializer(paths, factory);
        await initializer.InitializeAsync();
        return new(directory, paths.DatabasePath, factory, initializer, new SqliteMemoryStore(factory, Options.Create(new MemoryOptions { MaxDocuments = maxDocuments, MaxItemsPerDocument = maxItems, MaxQueryResults = 100 }), NullLogger<SqliteMemoryStore>.Instance));
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
