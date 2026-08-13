using System.Globalization;
using KernelOS.Core.Conversation;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConversationRecord = KernelOS.Core.Conversation.Conversation;

namespace KernelOS.Infrastructure.Conversation;

public sealed class SqliteConversationStore : IConversationStore
{
    private readonly ISqliteConnectionFactory connections;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SqliteConversationStore> logger;
    private readonly ConversationMemoryOptions options;

    public SqliteConversationStore(ISqliteConnectionFactory connections, IOptions<ConversationMemoryOptions> options, ILogger<SqliteConversationStore> logger, TimeProvider? timeProvider = null)
    {
        this.connections = connections;
        this.options = options.Value;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ConversationCreateResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        var now = timeProvider.GetUtcNow();
        var conversation = new ConversationRecord(Guid.NewGuid(), now, now, 1);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginImmediateAsync(connection, cancellationToken);
            try { await InsertConversationAsync(connection, conversation, cancellationToken); await CommitAsync(connection, cancellationToken); return new(ConversationStatus.Success, Copy(conversation)); }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    public async Task<ConversationGetResult> GetAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        if (conversationId == Guid.Empty) return new(ConversationStatus.InvalidRequest);
        try { await using var connection = await connections.OpenConnectionAsync(cancellationToken); var conversation = await ReadConversationAsync(connection, conversationId, cancellationToken); return conversation is null ? new(ConversationStatus.NotFound) : new(ConversationStatus.Success, conversation); }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    public async Task<ConversationListResult> ListAsync(ConversationListQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        if (query.Limit <= 0 || query.Offset < 0) return new(ConversationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, created_at_utc, updated_at_utc, version_number FROM conversations ORDER BY updated_at_utc DESC, id ASC LIMIT $limit OFFSET $offset;";
            command.Parameters.AddWithValue("$limit", Math.Min(query.Limit, options.MaxListPageSize)); command.Parameters.AddWithValue("$offset", query.Offset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken); var results = new List<ConversationRecord>();
            while (await reader.ReadAsync(cancellationToken)) results.Add(ReadConversation(reader));
            return new(ConversationStatus.Success, Array.AsReadOnly(results.ToArray()));
        }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    public async Task<ConversationAppendResult> AppendMessageAsync(AppendConversationMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        if (request.ConversationId == Guid.Empty || !IsRoleValid(request.Role) || string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > options.MaxMessageCharacters) return new(ConversationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken); await BeginImmediateAsync(connection, cancellationToken);
            try
            {
                var current = await ReadConversationAsync(connection, request.ConversationId, cancellationToken);
                if (current is null) { await RollbackAsync(connection); return new(ConversationStatus.NotFound); }
                var sequence = await NextSequenceAsync(connection, request.ConversationId, cancellationToken); var now = timeProvider.GetUtcNow();
                var message = new ConversationMessage(Guid.NewGuid(), request.ConversationId, sequence, request.Role, request.Content, now);
                var updated = current with { UpdatedAt = now, Version = current.Version + 1 };
                await InsertMessageAsync(connection, message, cancellationToken); await UpdateConversationAsync(connection, updated, cancellationToken); await CommitAsync(connection, cancellationToken);
                return new(ConversationStatus.Success, Copy(updated), Copy(message));
            }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    public async Task<ConversationMessagesResult> GetMessagesAsync(ConversationMessagesQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        if (query.ConversationId == Guid.Empty || query.Limit <= 0 || query.Offset < 0 || query.BeforeSequence is <= 0) return new(ConversationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            if (await ReadConversationAsync(connection, query.ConversationId, cancellationToken) is null) return new(ConversationStatus.NotFound);
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT id, conversation_id, sequence_number, role, content, created_at_utc FROM (SELECT id, conversation_id, sequence_number, role, content, created_at_utc FROM conversation_messages WHERE conversation_id = $conversationId AND ($beforeSequence IS NULL OR sequence_number < $beforeSequence) ORDER BY sequence_number DESC LIMIT $limit OFFSET $offset) ORDER BY sequence_number ASC;";
            command.Parameters.AddWithValue("$conversationId", query.ConversationId.ToString("D")); command.Parameters.AddWithValue("$beforeSequence", query.BeforeSequence is null ? DBNull.Value : query.BeforeSequence.Value); command.Parameters.AddWithValue("$limit", Math.Min(query.Limit, options.MaxMessagesPageSize)); command.Parameters.AddWithValue("$offset", query.Offset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken); var results = new List<ConversationMessage>(); while (await reader.ReadAsync(cancellationToken)) results.Add(ReadMessage(reader));
            return new(ConversationStatus.Success, Array.AsReadOnly(results.ToArray()));
        }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    public async Task<ConversationDeleteResult> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationStatus.Cancelled);
        if (conversationId == Guid.Empty) return new(ConversationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken); await BeginImmediateAsync(connection, cancellationToken);
            try { if (await DeleteConversationAsync(connection, conversationId, cancellationToken) == 0) { await RollbackAsync(connection); return new(ConversationStatus.NotFound); } await CommitAsync(connection, cancellationToken); return new(ConversationStatus.Success); }
            catch { await RollbackAsync(connection); throw; }
        }
        catch (OperationCanceledException) { return new(ConversationStatus.Cancelled); }
        catch (Exception) { ConversationStoreLog.Failed(logger); return new(ConversationStatus.Failed); }
    }

    private static bool IsRoleValid(ConversationRole role) => role is ConversationRole.User or ConversationRole.Assistant;
    private static ConversationRecord Copy(ConversationRecord value) => value with { };
    private static ConversationMessage Copy(ConversationMessage value) => value with { };
    private static async Task InsertConversationAsync(SqliteConnection c, ConversationRecord x, CancellationToken t) => await ExecuteAsync(c, "INSERT INTO conversations (id, created_at_utc, updated_at_utc, version_number) VALUES ($id, $created, $updated, $version);", cmd => { Add(cmd, "$id", x.Id); Add(cmd, "$created", x.CreatedAt); Add(cmd, "$updated", x.UpdatedAt); Add(cmd, "$version", x.Version); }, t);
    private static async Task InsertMessageAsync(SqliteConnection c, ConversationMessage x, CancellationToken t) => await ExecuteAsync(c, "INSERT INTO conversation_messages (id, conversation_id, sequence_number, role, content, created_at_utc) VALUES ($id, $conversation, $sequence, $role, $content, $created);", cmd => { Add(cmd, "$id", x.Id); Add(cmd, "$conversation", x.ConversationId); Add(cmd, "$sequence", x.Sequence); Add(cmd, "$role", (int)x.Role); Add(cmd, "$content", x.Content); Add(cmd, "$created", x.CreatedAt); }, t);
    private static async Task UpdateConversationAsync(SqliteConnection c, ConversationRecord x, CancellationToken t) => await ExecuteAsync(c, "UPDATE conversations SET updated_at_utc = $updated, version_number = $version WHERE id = $id;", cmd => { Add(cmd, "$id", x.Id); Add(cmd, "$updated", x.UpdatedAt); Add(cmd, "$version", x.Version); }, t);
    private static async Task<long> NextSequenceAsync(SqliteConnection c, Guid id, CancellationToken t) { await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COALESCE(MAX(sequence_number), 0) + 1 FROM conversation_messages WHERE conversation_id = $id;"; Add(cmd, "$id", id); return Convert.ToInt64(await cmd.ExecuteScalarAsync(t), CultureInfo.InvariantCulture); }
    private static async Task<int> DeleteConversationAsync(SqliteConnection c, Guid id, CancellationToken t) => await ExecuteAsync(c, "DELETE FROM conversations WHERE id = $id;", cmd => Add(cmd, "$id", id), t);
    private static async Task<ConversationRecord?> ReadConversationAsync(SqliteConnection c, Guid id, CancellationToken t) { await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT id, created_at_utc, updated_at_utc, version_number FROM conversations WHERE id = $id;"; Add(cmd, "$id", id); await using var r = await cmd.ExecuteReaderAsync(t); return await r.ReadAsync(t) ? ReadConversation(r) : null; }
    private static ConversationRecord ReadConversation(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), DateTimeOffset.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeOffset.Parse(r.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.GetInt64(3));
    private static ConversationMessage ReadMessage(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), r.GetInt64(2), (ConversationRole)r.GetInt32(3), r.GetString(4), DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    private static async Task<int> ExecuteAsync(SqliteConnection c, string sql, Action<SqliteCommand> bind, CancellationToken t) { await using var cmd = c.CreateCommand(); cmd.CommandText = sql; bind(cmd); return await cmd.ExecuteNonQueryAsync(t); }
    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value switch
    {
        Guid id => id.ToString("D"),
        DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
        null => DBNull.Value,
        _ => value
    });
    private static async Task BeginImmediateAsync(SqliteConnection c, CancellationToken t) => await ExecuteAsync(c, "BEGIN IMMEDIATE;", _ => { }, t);
    private static async Task CommitAsync(SqliteConnection c, CancellationToken t) => await ExecuteAsync(c, "COMMIT;", _ => { }, t);
    private static async Task RollbackAsync(SqliteConnection c) { try { await ExecuteAsync(c, "ROLLBACK;", _ => { }, CancellationToken.None); } catch (SqliteException) { } }
}

internal static partial class ConversationStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation store operation failed.")]
    internal static partial void Failed(ILogger logger);
}
