using KernelOS.Api.EndpointMappings;
using KernelOS.Infrastructure;
using KernelOS.Tools;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKernelTools();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

app.UseBlazorFrameworkFiles("/ui");
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapChatEndpoints();
app.MapKaiEndpoints();
app.MapConversationEndpoints();
app.MapToolEndpoints();
app.MapPlannerEndpoints();
app.MapExecutionEndpoints();
app.MapDocumentEndpoints();
app.MapFilesystemEndpoints();
app.MapFallbackToFile("/ui/{*path:nonfile}", "ui/index.html");

app.Run();

public partial class Program;

internal static partial class KernelOSLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unhandled exception while processing a request.")]
    public static partial void UnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A request body could not be read as JSON.")]
    public static partial void InvalidRequest(ILogger logger, BadHttpRequestException exception);
}
