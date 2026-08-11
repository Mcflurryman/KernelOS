using KernelOS.Api.Contracts;
using KernelOS.Core;

namespace KernelOS.Api.EndpointMappings;

public static class ChatEndpointMappings
{
    public static WebApplication MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/chat", async (
            ChatApiRequest? request,
            IChatModel chatModel,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return Results.BadRequest(new { error = "message is required." });
            }

            var response = await chatModel.SendAsync(
                new ChatRequest(request.Message, request.SystemPrompt, request.History),
                cancellationToken);

            if (response.Success)
            {
                return Results.Ok(response);
            }

            return response.Error switch
            {
                "service_unavailable" => Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
                "timeout" => Results.Json(response, statusCode: StatusCodes.Status504GatewayTimeout),
                "cancelled" => Results.Json(response, statusCode: 499),
                _ => Results.Json(response, statusCode: StatusCodes.Status502BadGateway)
            };
        });

        return app;
    }
}
