using System.Text.Json;
using KernelOS.Api.Contracts;
using KernelOS.Core;
using KernelOS.Tools;

namespace KernelOS.Api.EndpointMappings;

public static class DocumentEndpointMappings
{
    public static WebApplication MapDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/documents/read", async (
            DocumentReadApiRequest? request,
            IReadOnlyToolExecutionGateway toolExecutionGateway,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Path))
            {
                return Results.BadRequest(new { error = "path is required." });
            }

            var arguments = new Dictionary<string, JsonElement>
            {
                ["operation"] = JsonSerializer.SerializeToElement("read"),
                ["path"] = JsonSerializer.SerializeToElement(request.Path)
            };
            if (!string.IsNullOrWhiteSpace(request.Format))
            {
                arguments["format"] = JsonSerializer.SerializeToElement(request.Format);
            }

            var result = await toolExecutionGateway.ExecuteAsync(
                new ToolExecutionRequest("document", arguments),
                cancellationToken);
            return result.Status switch
            {
                ToolExecutionStatus.Success => Results.Ok(result),
                ToolExecutionStatus.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
                ToolExecutionStatus.NotFound => Results.NotFound(result),
                ToolExecutionStatus.Cancelled => Results.Json(result, statusCode: 499),
                ToolExecutionStatus.TooLarge => Results.Json(result, statusCode: StatusCodes.Status413PayloadTooLarge),
                ToolExecutionStatus.Failure => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError),
                _ => Results.BadRequest(result)
            };
        });

        return app;
    }
}
