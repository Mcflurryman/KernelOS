using System.Globalization;
using KernelOS.Core.Conversation;
using KernelOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Conversation;

public sealed class SqliteConversationExecutionCorrelationStore : IConversationExecutionCorrelationStore
{
    private readonly ISqliteConnectionFactory connections;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SqliteConversationExecutionCorrelationStore> logger;
    private readonly ConversationMemoryOptions options;

    public SqliteConversationExecutionCorrelationStore(
        ISqliteConnectionFactory connections,
        IOptions<ConversationMemoryOptions> options,
        ILogger<SqliteConversationExecutionCorrelationStore> logger,
        TimeProvider? timeProvider = null)
    {
        this.connections = connections;
        this.options = options.Value;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ConversationExecutionCorrelationRegisterResult> RegisterAsync(
        RegisterConversationExecutionCorrelationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationExecutionCorrelationStatus.Cancelled);
        if (!IsValid(request)) return new(ConversationExecutionCorrelationStatus.InvalidRequest);

        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            await BeginImmediateAsync(connection, cancellationToken);
            try
            {
                var existing = await ReadByPendingExecutionIdAsync(connection, request.PendingExecutionId, cancellationToken);
                if (existing is not null)
                {
                    await CommitAsync(connection, cancellationToken);
                    return SameIdentity(existing, request)
                        ? new(ConversationExecutionCorrelationStatus.Success, existing)
                        : new(ConversationExecutionCorrelationStatus.Conflict);
                }

                if (!await ConversationExistsAsync(connection, request.ConversationId, cancellationToken)
                    || !await MessageBelongsToConversationAsync(connection, request.UserMessageId, request.ConversationId, cancellationToken)
                    || request.AssistantMessageId is not null && !await MessageBelongsToConversationAsync(connection, request.AssistantMessageId.Value, request.ConversationId, cancellationToken))
                {
                    await RollbackAsync(connection);
                    return new(ConversationExecutionCorrelationStatus.NotFound);
                }

                var correlation = new ConversationExecutionCorrelation(
                    request.PendingExecutionId,
                    request.ConversationId,
                    request.UserMessageId,
                    request.AssistantMessageId,
                    timeProvider.GetUtcNow());
                await InsertAsync(connection, correlation, cancellationToken);
                await CommitAsync(connection, cancellationToken);
                return new(ConversationExecutionCorrelationStatus.Success, correlation);
            }
            catch
            {
                await RollbackAsync(connection);
                throw;
            }
        }
        catch (OperationCanceledException) { return new(ConversationExecutionCorrelationStatus.Cancelled); }
        catch (Exception) { ConversationExecutionCorrelationStoreLog.Failed(logger); return new(ConversationExecutionCorrelationStatus.Failed); }
    }

    public async Task<ConversationExecutionCorrelationGetResult> GetByPendingExecutionIdAsync(Guid pendingExecutionId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationExecutionCorrelationStatus.Cancelled);
        if (pendingExecutionId == Guid.Empty) return new(ConversationExecutionCorrelationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            var correlation = await ReadByPendingExecutionIdAsync(connection, pendingExecutionId, cancellationToken);
            return correlation is null
                ? new(ConversationExecutionCorrelationStatus.NotFound)
                : new(ConversationExecutionCorrelationStatus.Success, correlation);
        }
        catch (OperationCanceledException) { return new(ConversationExecutionCorrelationStatus.Cancelled); }
        catch (Exception) { ConversationExecutionCorrelationStoreLog.Failed(logger); return new(ConversationExecutionCorrelationStatus.Failed); }
    }

    public async Task<ConversationExecutionCorrelationListResult> ListByConversationAsync(ConversationExecutionCorrelationListQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ConversationExecutionCorrelationStatus.Cancelled);
        if (query.ConversationId == Guid.Empty || query.Limit <= 0 || query.Offset < 0) return new(ConversationExecutionCorrelationStatus.InvalidRequest);
        try
        {
            await using var connection = await connections.OpenConnectionAsync(cancellationToken);
            if (!await ConversationExistsAsync(connection, query.ConversationId, cancellationToken)) return new(ConversationExecutionCorrelationStatus.NotFound);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pending_execution_id, conversation_id, user_message_id, assistant_message_id, created_at_utc FROM conversation_pending_executions WHERE conversation_id = $conversationId ORDER BY created_at_utc DESC, pending_execution_id ASC LIMIT $limit OFFSET $offset;";
            Add(command, "$conversationId", query.ConversationId);
            command.Parameters.AddWithValue("$limit", Math.Min(query.Limit, options.MaxListPageSize));
            command.Parameters.AddWithValue("$offset", query.Offset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var correlations = new List<ConversationExecutionCorrelation>();
            while (await reader.ReadAsync(cancellationToken)) correlations.Add(Read(reader));
            return new(ConversationExecutionCorrelationStatus.Success, Array.AsReadOnly(correlations.ToArray()));
        }
        catch (OperationCanceledException) { return new(ConversationExecutionCorrelationStatus.Cancelled); }
        catch (Exception) { ConversationExecutionCorrelationStoreLog.Failed(logger); return new(ConversationExecutionCorrelationStatus.Failed); }
    }

    private static bool IsValid(RegisterConversationExecutionCorrelationRequest request) =>
        request.PendingExecutionId != Guid.Empty
        && request.ConversationId != Guid.Empty
        && request.UserMessageId != Guid.Empty
        && (!request.AssistantMessageId.HasValue || request.AssistantMessageId.Value != Guid.Empty);

    private static bool SameIdentity(ConversationExecutionCorrelation existing, RegisterConversationExecutionCorrelationRequest request) =>
        existing.ConversationId == request.ConversationId
        && existing.UserMessageId == request.UserMessageId
        && existing.AssistantMessageId == request.AssistantMessageId;

    private static async Task<bool> ConversationExistsAsync(SqliteConnection connection, Guid conversationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM conversations WHERE id = $conversationId);";
        Add(command, "$conversationId", conversationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> MessageBelongsToConversationAsync(SqliteConnection connection, Guid messageId, Guid conversationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM conversation_messages WHERE id = $messageId AND conversation_id = $conversationId);";
        Add(command, "$messageId", messageId);
        Add(command, "$conversationId", conversationId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task InsertAsync(SqliteConnection connection, ConversationExecutionCorrelation correlation, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO conversation_pending_executions (pending_execution_id, conversation_id, user_message_id, assistant_message_id, created_at_utc) VALUES ($pendingExecutionId, $conversationId, $userMessageId, $assistantMessageId, $createdAt);";
        Add(command, "$pendingExecutionId", correlation.PendingExecutionId);
        Add(command, "$conversationId", correlation.ConversationId);
        Add(command, "$userMessageId", correlation.UserMessageId);
        Add(command, "$assistantMessageId", correlation.AssistantMessageId);
        Add(command, "$createdAt", correlation.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ConversationExecutionCorrelation?> ReadByPendingExecutionIdAsync(SqliteConnection connection, Guid pendingExecutionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pending_execution_id, conversation_id, user_message_id, assistant_message_id, created_at_utc FROM conversation_pending_executions WHERE pending_execution_id = $pendingExecutionId;";
        Add(command, "$pendingExecutionId", pendingExecutionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static ConversationExecutionCorrelation Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        Guid.Parse(reader.GetString(2)),
        reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
        DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value switch
    {
        Guid id => id.ToString("D"),
        DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
        null => DBNull.Value,
        _ => value
    });

    private static async Task BeginImmediateAsync(SqliteConnection connection, CancellationToken cancellationToken) => await ExecuteAsync(connection, "BEGIN IMMEDIATE;", cancellationToken);
    private static async Task CommitAsync(SqliteConnection connection, CancellationToken cancellationToken) => await ExecuteAsync(connection, "COMMIT;", cancellationToken);
    private static async Task RollbackAsync(SqliteConnection connection) { try { await ExecuteAsync(connection, "ROLLBACK;", CancellationToken.None); } catch (SqliteException) { } }
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(cancellationToken); }
}

internal static partial class ConversationExecutionCorrelationStoreLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation execution correlation store operation failed.")]
    internal static partial void Failed(ILogger logger);
}
