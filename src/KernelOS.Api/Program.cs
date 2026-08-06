using KernelOS.Core;
using KernelOS.Api.Contracts;
using KernelOS.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
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
    Version: "0.1.0")));

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

app.Run();

public partial class Program;

internal static partial class KernelOSLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception while processing a request.")]
    public static partial void UnhandledException(ILogger logger, Exception? exception);
}
