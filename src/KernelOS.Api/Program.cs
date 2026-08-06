using KernelOS.Core;
using KernelOS.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddInfrastructure();

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

app.Run();

public partial class Program;

internal static partial class KernelOSLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception while processing a request.")]
    public static partial void UnhandledException(ILogger logger, Exception? exception);
}
