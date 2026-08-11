using KernelOS.Core;
using KernelOS.Infrastructure;
using Microsoft.Extensions.Options;

namespace KernelOS.Api.EndpointMappings;

public static class HealthEndpointMappings
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            message = "KernelOS is running.",
            assistant = "Kai"
        }));
        app.MapGet("/health", () => Results.Ok(new SystemStatusResponse(
            "ok",
            "KernelOS",
            "Kai",
            GetProductVersion())));
        app.MapGet("/health/ollama", async (
            IOllamaHealthCheck healthCheck,
            IOptions<OllamaOptions> options,
            CancellationToken cancellationToken) =>
        {
            var available = await healthCheck.IsAvailableAsync(cancellationToken);
            return available
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

        return app;
    }

    private static string GetProductVersion()
    {
        var version = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .SingleOrDefault()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version)
            ? "unknown"
            : version.Split('+', 2)[0];
    }
}
