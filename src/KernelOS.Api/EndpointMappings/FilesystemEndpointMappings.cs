using System.Text.Json;
using KernelOS.Api.Contracts;
using KernelOS.Core;
using KernelOS.Tools;

namespace KernelOS.Api.EndpointMappings;

public static class FilesystemEndpointMappings
{
    private static readonly HashSet<string> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        "search",
        "exists",
        "metadata",
        "resolve",
        "list"
    };

    public static WebApplication MapFilesystemEndpoints(this WebApplication app)
    {
        app.MapPost("/filesystem/{operation}", async (
            string operation,
            ToolExecutionApiRequest? request,
            IReadOnlyToolExecutionGateway toolExecutionGateway,
            CancellationToken cancellationToken) =>
        {
            if (!Operations.Contains(operation))
            {
                return Results.BadRequest(new { error = "Invalid filesystem operation." });
            }

            var arguments = new Dictionary<string, JsonElement>(
                request?.Arguments ?? new Dictionary<string, JsonElement>())
            {
                ["operation"] = JsonSerializer.SerializeToElement(operation)
            };
            var result = await toolExecutionGateway.ExecuteAsync(
                new ToolExecutionRequest("filesystem", arguments),
                cancellationToken);

            return result.Status == ToolExecutionStatus.Success
                ? Results.Ok(result)
                : result.Status == ToolExecutionStatus.Unauthorized
                    ? Results.Json(result, statusCode: 403)
                    : result.Status == ToolExecutionStatus.NotFound
                        ? Results.NotFound(result)
                        : Results.BadRequest(result);
        });

        return app;
    }
}
