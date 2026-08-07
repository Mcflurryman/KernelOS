using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Api.Contracts;
using KernelOS.Infrastructure;
using KernelOS.Tools;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKernelTools();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is BadHttpRequestException badRequestException)
        {
            KernelOSLog.InvalidRequest(app.Logger, badRequestException);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid JSON request body." });
            return;
        }

        KernelOSLog.UnhandledException(app.Logger, exception);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.MapGet("/", () => Results.Ok(new
{
    message = "KernelOS is running.",
    assistant = "Kai"
}));

app.MapGet("/health", () => Results.Ok(new SystemStatusResponse(
    Status: "ok",
    Application: "KernelOS",
    Assistant: "Kai",
    Version: GetProductVersion())));

app.MapGet("/health/ollama", async (
    IOllamaHealthCheck healthCheck,
    IOptions<OllamaOptions> options,
    CancellationToken cancellationToken) =>
{
    var isAvailable = await healthCheck.IsAvailableAsync(cancellationToken);

    return isAvailable
        ? Results.Ok(new
        {
            status = "ok",
            service = "ollama",
            baseUrl = options.Value.BaseUrl,
            model = options.Value.Model
        })
        : Results.Json(new
        {
            status = "unavailable",
            service = "ollama",
            model = options.Value.Model
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

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
    IToolRouter toolRouter,
    CancellationToken cancellationToken) =>
{
    var arguments = request?.Arguments ?? new Dictionary<string, System.Text.Json.JsonElement>();
    var result = await toolRouter.ExecuteAsync(
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

app.MapPost("/planner/execute", async (PlannerExecuteApiRequest? request, IPlanner planner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request?.Goal) || string.IsNullOrWhiteSpace(request.Tool)) return Results.BadRequest(new { error = "goal and tool are required." });
    var metadata = new Dictionary<string, System.Text.Json.JsonElement>
    {
        ["tool"] = System.Text.Json.JsonSerializer.SerializeToElement(request.Tool),
        ["arguments"] = System.Text.Json.JsonSerializer.SerializeToElement(request.Arguments ?? new Dictionary<string, System.Text.Json.JsonElement>())
    };
    var result = await planner.PlanAsync(new Goal(Guid.NewGuid(), request.Goal, DateTimeOffset.UtcNow, 0, metadata), cancellationToken);
    return result.Status switch
    {
        PlannerStatus.Completed => Results.Ok(result),
        PlannerStatus.Cancelled => Results.Json(result, statusCode: 499),
        _ => Results.BadRequest(result)
    };
});

app.MapPost("/documents/read", async (DocumentReadApiRequest? request, IToolRouter router, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request?.Path)) return Results.BadRequest(new { error = "path is required." });
    var arguments = new Dictionary<string, System.Text.Json.JsonElement>
    {
        ["operation"] = System.Text.Json.JsonSerializer.SerializeToElement("read"),
        ["path"] = System.Text.Json.JsonSerializer.SerializeToElement(request.Path)
    };
    if (!string.IsNullOrWhiteSpace(request.Format)) arguments["format"] = System.Text.Json.JsonSerializer.SerializeToElement(request.Format);
    var result = await router.ExecuteAsync(new ToolExecutionRequest("document", arguments), cancellationToken);
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

var filesystemOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "search", "exists", "metadata", "resolve", "list" };
app.MapPost("/filesystem/{operation}", async (string operation, ToolExecutionApiRequest? request, IToolRouter router, CancellationToken cancellationToken) =>
{
    if (!filesystemOperations.Contains(operation)) return Results.BadRequest(new { error = "Invalid filesystem operation." });
    var args = new Dictionary<string,System.Text.Json.JsonElement>(request?.Arguments ?? new Dictionary<string,System.Text.Json.JsonElement>()) { ["operation"] = System.Text.Json.JsonSerializer.SerializeToElement(operation) };
    var result = await router.ExecuteAsync(new ToolExecutionRequest("filesystem",args),cancellationToken);
    return result.Status==ToolExecutionStatus.Success?Results.Ok(result):result.Status==ToolExecutionStatus.Unauthorized?Results.Json(result,statusCode:403):result.Status==ToolExecutionStatus.NotFound?Results.NotFound(result):Results.BadRequest(result);
});

app.Run();

static object CreateToolDescription(IKernelTool tool) => new
{
    name = tool.Name,
    description = tool.Description,
    category = tool.Category,
    capabilities = tool.Capabilities,
    parameters = tool.Parameters
};

static string GetProductVersion()
{
    var informationalVersion = typeof(Program).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .SingleOrDefault()
        ?.InformationalVersion;

    return string.IsNullOrWhiteSpace(informationalVersion)
        ? "unknown"
        : informationalVersion.Split('+', 2)[0];
}

public partial class Program;

internal static partial class KernelOSLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception while processing a request.")]
    public static partial void UnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A request body could not be read as JSON.")]
    public static partial void InvalidRequest(ILogger logger, BadHttpRequestException exception);
}
