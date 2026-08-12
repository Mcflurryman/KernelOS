using System.Globalization;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Memory;

public sealed class SqliteMemoryStore(
    ISqliteConnectionFactory connections,
    IOptions<MemoryOptions> options,
    ILogger<SqliteMemoryStore> logger) : IMemoryStore
{
    private readonly MemoryOptionsSnapshot limits = new(options.Value.MaxDocuments, options.Value.MaxItemsPerDocument, options.Value.MaxQueryResults);

    public async Task<MemoryStoreResult> StoreAsync(MemoryStoreRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (request.KnowledgeDocument is null || request.KnowledgeDocument.Items.Count > limits.MaxItemsPerDocument)
            return new(MemoryStatus.InvalidRequest, Error: "The memory store request is invalid.");

        var document = MemoryDocumentFactory.Create(request.KnowledgeDocument, DateTimeOffset.UtcNow);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginImmediateAsync(connection, cancellationToken);
            try
            {
                if (await CountAsync(connection, cancellationToken) >= limits.MaxDocuments)
                {
                    await RollbackAsync(connection);
                    return new(MemoryStatus.InvalidRequest, Error: "The memory document limit was reached.");
                }
                if (await ExistsKnowledgeDocumentAsync(connection, document.KnowledgeDocumentId, cancellationToken))
                {
                    await RollbackAsync(connection);
                    return new(MemoryStatus.AlreadyExists);
                }

                await InsertDocumentAsync(connection, document, cancellationToken);
                await CommitAsync(connection, cancellationToken);
                return new(MemoryStatus.Success, MemoryDocumentFactory.Copy(document));
            }
            catch
            {
                await RollbackAsync(connection);
                throw;
            }
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch (SqliteException exception) when (IsUniqueViolation(exception)) { return new(MemoryStatus.AlreadyExists); }
        catch (Exception)
        {
            SqliteMemoryStoreLog.StoreFailed(logger);
            return new(MemoryStatus.Failed, Error: "Memory storage failed.");
        }
    }

    public async Task<MemoryGetResult> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (!Guid.TryParse(id, out var documentId)) return new(MemoryStatus.NotFound);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginDeferredAsync(connection, cancellationToken);
            try
            {
                var document = await ReadDocumentAsync(connection, documentId, cancellationToken);
                await CommitAsync(connection, cancellationToken);
                return document is null ? new(MemoryStatus.NotFound) : new(MemoryStatus.Success, document);
            }
            catch
            {
                await RollbackAsync(connection);
                throw;
            }
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch (Exception)
        {
            SqliteMemoryStoreLog.GetFailed(logger);
            return new(MemoryStatus.Failed, Error: "Memory retrieval failed.");
        }
    }

    public async Task<MemoryUpdateResult> UpdateAsync(MemoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (!Guid.TryParse(request.Id, out var id) || request.Items is null || request.Metadata is null || request.Items.Count > limits.MaxItemsPerDocument)
            return new(MemoryStatus.InvalidRequest, Error: "The memory update request is invalid.");
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginImmediateAsync(connection, cancellationToken);
            try
            {
                var current = await ReadDocumentAsync(connection, id, cancellationToken);
                if (current is null) { await RollbackAsync(connection); return new(MemoryStatus.NotFound); }
                var updated = MemoryDocumentFactory.Update(current, request.Items, request.Metadata, DateTimeOffset.UtcNow);
                await DeleteAggregateChildrenAsync(connection, id, cancellationToken);
                await UpdateDocumentAsync(connection, updated, cancellationToken);
                await InsertMetadataAsync(connection, "memory_document_metadata", id, null, updated.Metadata.Properties, cancellationToken);
                await InsertItemsAsync(connection, updated, cancellationToken);
                await CommitAsync(connection, cancellationToken);
                return new(MemoryStatus.Success, MemoryDocumentFactory.Copy(updated));
            }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch (Exception) { SqliteMemoryStoreLog.UpdateFailed(logger); return new(MemoryStatus.Failed, Error: "Memory update failed."); }
    }

    public async Task<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (!Guid.TryParse(request.Id, out var id)) return new(MemoryStatus.InvalidRequest, "The memory delete request is invalid.");
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginImmediateAsync(connection, cancellationToken);
            try
            {
                var deleted = await DeleteDocumentAsync(connection, id, cancellationToken);
                if (deleted == 0) { await RollbackAsync(connection); return new(MemoryStatus.NotFound); }
                await CommitAsync(connection, cancellationToken);
                return new(MemoryStatus.Success);
            }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch (Exception) { SqliteMemoryStoreLog.DeleteFailed(logger); return new(MemoryStatus.Failed, "Memory delete failed."); }
    }

    public async Task<MemoryQueryResult> QueryAsync(MemoryQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(MemoryStatus.Cancelled);
        if (query.Limit <= 0 || query.Offset < 0) return new(MemoryStatus.InvalidRequest, Error: "The memory query is invalid.");
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginDeferredAsync(connection, cancellationToken);
            try
            {
                var documents = new List<MemoryDocument>();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT id FROM memory_documents ORDER BY updated_at_utc DESC, id ASC;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var ids = new List<Guid>();
                while (await reader.ReadAsync(cancellationToken)) ids.Add(ReadGuid(reader, 0));
                await reader.DisposeAsync();
                foreach (var id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var document = await ReadDocumentAsync(connection, id, cancellationToken);
                    if (document is not null && Matches(document, query)) documents.Add(document);
                }
                await CommitAsync(connection, cancellationToken);
                return new(MemoryStatus.Success, documents.Skip(query.Offset).Take(Math.Min(query.Limit, limits.MaxQueryResults)).Select(MemoryDocumentFactory.Copy).ToArray());
            }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(MemoryStatus.Cancelled); }
        catch (Exception) { SqliteMemoryStoreLog.QueryFailed(logger); return new(MemoryStatus.Failed, Error: "Memory query failed."); }
    }

    private static async Task InsertDocumentAsync(SqliteConnection connection, MemoryDocument document, CancellationToken token)
    {
        await ExecuteAsync(connection, "INSERT INTO memory_documents (id, knowledge_document_id, created_at_utc, updated_at_utc, version_number, version_updated_at_utc, version_content_hash, content_hash, mime_type, format, language) VALUES ($id, $knowledgeDocumentId, $createdAt, $updatedAt, $versionNumber, $versionUpdatedAt, $versionContentHash, $contentHash, $mimeType, $format, $language);", command =>
        {
            Add(command, "$id", document.Id); Add(command, "$knowledgeDocumentId", document.KnowledgeDocumentId); Add(command, "$createdAt", document.CreatedAt); Add(command, "$updatedAt", document.UpdatedAt); Add(command, "$versionNumber", document.Version.Number); Add(command, "$versionUpdatedAt", document.Version.UpdatedAt); Add(command, "$versionContentHash", document.Version.ContentHash); Add(command, "$contentHash", document.ContentHash); Add(command, "$mimeType", document.Metadata.MimeType); Add(command, "$format", document.Metadata.Format); Add(command, "$language", document.Metadata.Language);
        }, token);
        await InsertMetadataAsync(connection, "memory_document_metadata", document.Id, null, document.Metadata.Properties, token);
        await InsertItemsAsync(connection, document, token);
    }

    private static async Task InsertItemsAsync(SqliteConnection connection, MemoryDocument document, CancellationToken token)
    {
        for (var ordinal = 0; ordinal < document.Items.Count; ordinal++)
        {
            var item = document.Items[ordinal]; var locator = item.Source.Locator;
            await ExecuteAsync(connection, "INSERT INTO memory_items (document_id, ordinal, item_id, knowledge_item_id, item_type, content, content_hash, mime_type, format, language, source_document_id, safe_reference, display_reference, locator_section_id, locator_line, locator_column, locator_row, locator_json_path, locator_description) VALUES ($documentId, $ordinal, $itemId, $knowledgeItemId, $itemType, $content, $contentHash, $mimeType, $format, $language, $sourceDocumentId, $safeReference, $displayReference, $sectionId, $line, $column, $row, $jsonPath, $description);", command =>
            {
                Add(command, "$documentId", document.Id); Add(command, "$ordinal", ordinal); Add(command, "$itemId", item.Id); Add(command, "$knowledgeItemId", item.KnowledgeItemId); Add(command, "$itemType", (int)item.Type); Add(command, "$content", item.Content); Add(command, "$contentHash", item.ContentHash); Add(command, "$mimeType", item.Metadata.MimeType); Add(command, "$format", item.Metadata.Format); Add(command, "$language", item.Metadata.Language); Add(command, "$sourceDocumentId", item.Source.DocumentId); Add(command, "$safeReference", item.Source.SafeReference); Add(command, "$displayReference", item.Source.DisplayReference); Add(command, "$sectionId", locator?.SectionId); Add(command, "$line", locator?.Line); Add(command, "$column", locator?.Column); Add(command, "$row", locator?.Row); Add(command, "$jsonPath", locator?.JsonPath); Add(command, "$description", locator?.Description);
            }, token);
            await InsertMetadataAsync(connection, "memory_item_metadata", document.Id, ordinal, item.Metadata.Properties, token);
        }
    }

    private static async Task UpdateDocumentAsync(SqliteConnection connection, MemoryDocument document, CancellationToken token) =>
        await ExecuteAsync(connection, "UPDATE memory_documents SET updated_at_utc = $updatedAt, version_number = $versionNumber, version_updated_at_utc = $versionUpdatedAt, version_content_hash = $versionContentHash, content_hash = $contentHash, mime_type = $mimeType, format = $format, language = $language WHERE id = $id;", command =>
        { Add(command, "$id", document.Id); Add(command, "$updatedAt", document.UpdatedAt); Add(command, "$versionNumber", document.Version.Number); Add(command, "$versionUpdatedAt", document.Version.UpdatedAt); Add(command, "$versionContentHash", document.Version.ContentHash); Add(command, "$contentHash", document.ContentHash); Add(command, "$mimeType", document.Metadata.MimeType); Add(command, "$format", document.Metadata.Format); Add(command, "$language", document.Metadata.Language); }, token);

    private static async Task DeleteAggregateChildrenAsync(SqliteConnection connection, Guid id, CancellationToken token)
    {
        await ExecuteAsync(connection, "DELETE FROM memory_document_metadata WHERE document_id = $id;", command => Add(command, "$id", id), token);
        await ExecuteAsync(connection, "DELETE FROM memory_items WHERE document_id = $id;", command => Add(command, "$id", id), token);
    }

    private static async Task<int> DeleteDocumentAsync(SqliteConnection connection, Guid id, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM memory_documents WHERE id = $id;"; Add(command, "$id", id);
        return await command.ExecuteNonQueryAsync(token);
    }

    private static bool Matches(MemoryDocument document, MemoryQuery query) =>
        (query.Id is null || string.Equals(document.Id.ToString(), query.Id, StringComparison.OrdinalIgnoreCase))
        && (!query.KnowledgeDocumentId.HasValue || document.KnowledgeDocumentId == query.KnowledgeDocumentId)
        && (!query.MemoryItemId.HasValue || document.Items.Any(item => item.Id == query.MemoryItemId))
        && (query.ContentHash is null || document.ContentHash == query.ContentHash || document.Items.Any(item => item.ContentHash == query.ContentHash))
        && (query.ItemType is null || document.Items.Any(item => item.Type == query.ItemType))
        && (query.ExactContent is null || document.Items.Any(item => item.Content == query.ExactContent))
        && (query.MetadataKey is null || document.Metadata.Properties?.TryGetValue(query.MetadataKey, out var value) == true && value == query.MetadataValue);

    private static async Task<MemoryDocument?> ReadDocumentAsync(SqliteConnection connection, Guid id, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT id, knowledge_document_id, created_at_utc, updated_at_utc, version_number, version_updated_at_utc, version_content_hash, content_hash, mime_type, format, language FROM memory_documents WHERE id = $id;"; Add(command, "$id", id);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var documentId = ReadGuid(reader, 0); var knowledgeId = ReadGuid(reader, 1); var created = ReadTimestamp(reader, 2); var updated = ReadTimestamp(reader, 3); var versionNumber = reader.GetInt32(4); var versionUpdated = ReadTimestamp(reader, 5); var versionHash = reader.GetString(6); var contentHash = reader.GetString(7); var metadata = new KnowledgeMetadata(reader.GetString(8), ReadNullable(reader, 9), ReadNullable(reader, 10), await ReadMetadataAsync(connection, "memory_document_metadata", documentId, null, token));
        await reader.DisposeAsync();
        var items = await ReadItemsAsync(connection, documentId, token);
        return new MemoryDocument(documentId, knowledgeId, created, updated, new(versionNumber, versionUpdated, versionHash), items, metadata, contentHash);
    }

    private static async Task<IReadOnlyList<MemoryItem>> ReadItemsAsync(SqliteConnection connection, Guid documentId, CancellationToken token)
    {
        var rows = new List<(int Ordinal, Guid Id, Guid KnowledgeId, KnowledgeItemType Type, string Content, string ContentHash, string MimeType, string? Format, string? Language, KnowledgeSource Source)>();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT ordinal, item_id, knowledge_item_id, item_type, content, content_hash, mime_type, format, language, source_document_id, safe_reference, display_reference, locator_section_id, locator_line, locator_column, locator_row, locator_json_path, locator_description FROM memory_items WHERE document_id = $documentId ORDER BY ordinal ASC;"; Add(command, "$documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var ordinal = reader.GetInt32(0); var typeValue = reader.GetInt32(3); if (!Enum.IsDefined(typeof(KnowledgeItemType), typeValue)) throw new InvalidDataException("The persisted memory item type is invalid.");
            var locator = reader.IsDBNull(12) && reader.IsDBNull(13) && reader.IsDBNull(14) && reader.IsDBNull(15) && reader.IsDBNull(16) && reader.IsDBNull(17) ? null : new KnowledgeLocator(ReadNullable(reader, 12), ReadNullableInt(reader, 13), ReadNullableInt(reader, 14), ReadNullableInt(reader, 15), ReadNullable(reader, 16), ReadNullable(reader, 17));
            var source = new KnowledgeSource(ReadGuid(reader, 9), reader.GetString(10), reader.GetString(11), locator);
            rows.Add((ordinal, ReadGuid(reader, 1), ReadGuid(reader, 2), (KnowledgeItemType)typeValue, reader.GetString(4), reader.GetString(5), reader.GetString(6), ReadNullable(reader, 7), ReadNullable(reader, 8), source));
        }
        await reader.DisposeAsync();
        var items = new List<MemoryItem>();
        foreach (var row in rows)
            items.Add(new MemoryItem(row.Id, row.KnowledgeId, row.Type, row.Content, row.Source, new(row.MimeType, row.Format, row.Language, await ReadMetadataAsync(connection, "memory_item_metadata", documentId, row.Ordinal, token)), row.ContentHash));
        return items;
    }

    private static async Task<IReadOnlyDictionary<string, string>?> ReadMetadataAsync(SqliteConnection connection, string table, Guid documentId, int? ordinal, CancellationToken token)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal); await using var command = connection.CreateCommand(); command.CommandText = ordinal is null ? "SELECT key, value FROM memory_document_metadata WHERE document_id = $documentId ORDER BY key;" : "SELECT key, value FROM memory_item_metadata WHERE document_id = $documentId AND item_ordinal = $ordinal ORDER BY key;"; Add(command, "$documentId", documentId); if (ordinal is not null) Add(command, "$ordinal", ordinal.Value);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) values.Add(reader.GetString(0), reader.GetString(1)); return values.Count == 0 ? null : values;
    }

    private static async Task InsertMetadataAsync(SqliteConnection connection, string table, Guid documentId, int? ordinal, IReadOnlyDictionary<string, string>? metadata, CancellationToken token)
    {
        if (metadata is null) return;
        foreach (var pair in metadata)
        {
            var sql = ordinal is null ? "INSERT INTO memory_document_metadata (document_id, key, value) VALUES ($documentId, $key, $value);" : "INSERT INTO memory_item_metadata (document_id, item_ordinal, key, value) VALUES ($documentId, $ordinal, $key, $value);";
            await ExecuteAsync(connection, sql, command => { Add(command, "$documentId", documentId); if (ordinal is not null) Add(command, "$ordinal", ordinal.Value); Add(command, "$key", pair.Key); Add(command, "$value", pair.Value); }, token);
        }
    }

    private static async Task<long> CountAsync(SqliteConnection connection, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM memory_documents;"; return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture); }
    private static async Task<bool> ExistsKnowledgeDocumentAsync(SqliteConnection connection, Guid id, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "SELECT EXISTS(SELECT 1 FROM memory_documents WHERE knowledge_document_id = $id);"; Add(command, "$id", id); return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) != 0; }
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, Action<SqliteCommand> configure, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = sql; configure(command); await command.ExecuteNonQueryAsync(token); }
    private static async Task BeginImmediateAsync(SqliteConnection connection, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "BEGIN IMMEDIATE;"; await command.ExecuteNonQueryAsync(token); }
    private static async Task BeginDeferredAsync(SqliteConnection connection, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "BEGIN;"; await command.ExecuteNonQueryAsync(token); }
    private static async Task CommitAsync(SqliteConnection connection, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "COMMIT;"; await command.ExecuteNonQueryAsync(token); }
    private static async Task RollbackAsync(SqliteConnection connection) { await using var command = connection.CreateCommand(); command.CommandText = "ROLLBACK;"; try { await command.ExecuteNonQueryAsync(CancellationToken.None); } catch (SqliteException) { } }
    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value switch { Guid id => id.ToString("D"), DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture), null => DBNull.Value, _ => value });
    private static Guid ReadGuid(SqliteDataReader reader, int ordinal) => Guid.TryParseExact(reader.GetString(ordinal), "D", out var value) ? value : throw new InvalidDataException("The persisted memory identifier is invalid.");
    private static DateTimeOffset ReadTimestamp(SqliteDataReader reader, int ordinal) => DateTimeOffset.TryParseExact(reader.GetString(ordinal), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : throw new InvalidDataException("The persisted memory timestamp is invalid.");
    private static string? ReadNullable(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static int? ReadNullableInt(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static bool IsUniqueViolation(SqliteException exception) =>
        exception.SqliteErrorCode == 19
        && exception.SqliteExtendedErrorCode is 1555 or 2067;
}

internal static partial class SqliteMemoryStoreLog
{
    [LoggerMessage(EventId = 40, Level = LogLevel.Error, Message = "SQLite memory storage failed.")]
    internal static partial void StoreFailed(ILogger logger);
    [LoggerMessage(EventId = 41, Level = LogLevel.Error, Message = "SQLite memory retrieval failed.")]
    internal static partial void GetFailed(ILogger logger);
    [LoggerMessage(EventId = 42, Level = LogLevel.Error, Message = "SQLite memory update failed.")]
    internal static partial void UpdateFailed(ILogger logger);
    [LoggerMessage(EventId = 43, Level = LogLevel.Error, Message = "SQLite memory delete failed.")]
    internal static partial void DeleteFailed(ILogger logger);
    [LoggerMessage(EventId = 44, Level = LogLevel.Error, Message = "SQLite memory query failed.")]
    internal static partial void QueryFailed(ILogger logger);
}
