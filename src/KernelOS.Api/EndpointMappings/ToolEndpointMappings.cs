using KernelOS.Api.Contracts;
using KernelOS.Core;
using KernelOS.Tools;

namespace KernelOS.Api.EndpointMappings;

public static class ToolEndpointMappings
{
    public static WebApplication MapToolEndpoints(this WebApplication app)
    {
        app.MapGet("/tools", (IToolRegistry toolRegistry) =>
            Results.Ok(toolRegistry.Tools.Select(CreateToolDescription)));

        app.MapGet("/tools/{name}", (string name, IToolRegistry toolRegistry) =>
        {
            var tool = toolRegistry.GetByName(name);
            return tool is null
                ? Results.NotFound(new { error = "The requested tool is not registered." })
                : Results.Ok(CreateToolDescription(tool));
        });

        app.MapPost("/tools/{name}", async (
            string name,
            ToolExecutionApiRequest? request,
            IReadOnlyToolExecutionGateway toolExecutionGateway,
            CancellationToken cancellationToken) =>
        {
            var arguments = request?.Arguments ?? new Dictionary<string, System.Text.Json.JsonElement>();
            var result = await toolExecutionGateway.ExecuteAsync(
                new ToolExecutionRequest(name, arguments),
                cancellationToken);

            return result.Status switch
            {
                ToolExecutionStatus.Success => Results.Ok(result),
                ToolExecutionStatus.InvalidArguments => Results.BadRequest(result),
                ToolExecutionStatus.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
                ToolExecutionStatus.NotFound => Results.NotFound(result),
                ToolExecutionStatus.Cancelled => Results.Json(result, statusCode: 499),
                _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static object CreateToolDescription(IKernelTool tool) => new
    {
        name = tool.Name,
        description = tool.Description,
        category = tool.Category,
        capabilities = tool.Capabilities,
        parameters = tool.Parameters,
        executionMetadata = tool.ExecutionMetadata
    };
}
