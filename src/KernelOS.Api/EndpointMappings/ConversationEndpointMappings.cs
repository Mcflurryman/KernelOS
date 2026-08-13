using KernelOS.Api.Contracts;
using KernelOS.Core.Conversation;
using KernelOS.Core.Kai;
using KernelOS.Infrastructure.Conversation;
using Microsoft.Extensions.Options;

namespace KernelOS.Api.EndpointMappings;

public static class ConversationEndpointMappings
{
    public static WebApplication MapConversationEndpoints(this WebApplication app)
    {
        app.MapPost("/conversations", async (IConversationStore store, CancellationToken cancellationToken) =>
        {
            var result = await store.CreateAsync(cancellationToken);
            return result.Status switch
            {
                ConversationStatus.Success => Results.Created($"/conversations/{result.Conversation!.Id}", ToResponse(result.Conversation)),
                ConversationStatus.Cancelled => Results.Json(new { error = "Request cancelled." }, statusCode: 499),
                _ => Results.Json(new { error = "Conversation could not be created." }, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        app.MapGet("/conversations", async (int? limit, int? offset, IConversationStore store, IOptions<ConversationMemoryOptions> options, CancellationToken cancellationToken) =>
        {
            var requestedLimit = limit ?? 50; var requestedOffset = offset ?? 0;
            if (requestedLimit <= 0 || requestedLimit > options.Value.MaxListPageSize || requestedOffset < 0) return Results.BadRequest(new { error = "Invalid paging parameters." });
            var result = await store.ListAsync(new(requestedLimit, requestedOffset), cancellationToken);
            return result.Status switch
            {
                ConversationStatus.Success => Results.Ok(result.Conversations!.Select(ToResponse)),
                ConversationStatus.Cancelled => Results.Json(new { error = "Request cancelled." }, statusCode: 499),
                _ => Results.Json(new { error = "Conversations could not be loaded." }, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        app.MapGet("/conversations/{id:guid}", async (Guid id, IConversationStore store, CancellationToken cancellationToken) =>
        {
            if (id == Guid.Empty) return Results.BadRequest(new { error = "Invalid conversation id." });
            var result = await store.GetAsync(id, cancellationToken);
            return result.Status switch
            {
                ConversationStatus.Success => Results.Ok(ToResponse(result.Conversation!)),
                ConversationStatus.NotFound => Results.NotFound(),
                ConversationStatus.Cancelled => Results.Json(new { error = "Request cancelled." }, statusCode: 499),
                ConversationStatus.InvalidRequest => Results.BadRequest(new { error = "Conversation could not be loaded." }),
                _ => Results.Json(new { error = "Conversation could not be loaded." }, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        app.MapGet("/conversations/{id:guid}/messages", async (Guid id, int? limit, int? offset, long? beforeSequence, IConversationStore store, IOptions<ConversationMemoryOptions> options, CancellationToken cancellationToken) =>
        {
            var requestedLimit = limit ?? 50; var requestedOffset = offset ?? 0;
            if (id == Guid.Empty || requestedLimit <= 0 || requestedLimit > options.Value.MaxMessagesPageSize || requestedOffset < 0) return Results.BadRequest(new { error = "Invalid conversation or paging parameters." });
            var result = await store.GetMessagesAsync(new(id, requestedLimit, requestedOffset, beforeSequence), cancellationToken);
            return result.Status switch
            {
                ConversationStatus.Success => Results.Ok(result.Messages!.Select(ToResponse)),
                ConversationStatus.NotFound => Results.NotFound(),
                ConversationStatus.Cancelled => Results.Json(new { error = "Request cancelled." }, statusCode: 499),
                ConversationStatus.InvalidRequest => Results.BadRequest(new { error = "Messages could not be loaded." }),
                _ => Results.Json(new { error = "Messages could not be loaded." }, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        app.MapPost("/conversations/{id:guid}/messages", async (Guid id, ConversationTurnApiRequest? request, IConversationTurnOrchestrator turns, CancellationToken cancellationToken) =>
        {
            if (id == Guid.Empty || request is null) return Results.BadRequest();
            var result = await turns.HandleAsync(new(id, request.Message, request.PreferredMode, request.ToolName, request.Arguments), cancellationToken);
            var response = ToResponse(result);
            return result.Status switch
            {
                ConversationTurnStatus.Success or ConversationTurnStatus.PartialSuccess => Results.Ok(response),
                ConversationTurnStatus.ConfirmationRequired => Results.Json(response, statusCode: StatusCodes.Status409Conflict),
                ConversationTurnStatus.NotFound => Results.NotFound(response),
                ConversationTurnStatus.Cancelled => Results.Json(response, statusCode: 499),
                ConversationTurnStatus.InvalidRequest => Results.BadRequest(response),
                _ => Results.Json(response, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        app.MapDelete("/conversations/{id:guid}", async (Guid id, IConversationStore store, CancellationToken cancellationToken) =>
        {
            if (id == Guid.Empty) return Results.BadRequest(new { error = "Invalid conversation id." });
            var result = await store.DeleteAsync(id, cancellationToken);
            return result.Status switch
            {
                ConversationStatus.Success => Results.NoContent(),
                ConversationStatus.NotFound => Results.NotFound(),
                ConversationStatus.Cancelled => Results.Json(new { error = "Request cancelled." }, statusCode: 499),
                ConversationStatus.InvalidRequest => Results.BadRequest(new { error = "Conversation could not be deleted." }),
                _ => Results.Json(new { error = "Conversation could not be deleted." }, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static ConversationApiResponse ToResponse(Conversation conversation) => new(conversation.Id, conversation.CreatedAt, conversation.UpdatedAt, conversation.Version);
    private static ConversationMessageApiResponse ToResponse(ConversationMessage message) => new(message.Id, message.ConversationId, message.Sequence, message.Role == ConversationRole.User ? "user" : "assistant", message.Content, message.CreatedAt);
    private static ConversationTurnApiResponse ToResponse(ConversationTurnResult result)
    {
        var kai = result.KaiResponse;
        return new(result.ConversationId, result.UserMessageId, result.AssistantMessageId, result.Status.ToString(), kai?.Status.ToString(), kai?.ModeUsed.ToString(), kai?.Answer, kai?.Citations, kai?.Warnings, kai?.Model, kai?.PendingExecutionId, kai?.Confirmation, result.ErrorCode);
    }
}
