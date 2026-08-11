using KernelOS.Core.Embeddings;
using KernelOS.Infrastructure.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure;

internal static class EmbeddingInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddEmbeddingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection(EmbeddingOptions.SectionName))
            .Validate(
                options => options.MaxInputCharacters > 0
                    && options.MaxBatchSize > 0
                    && options.ExpectedDimensions > 0
                    && options.TimeoutSeconds > 0
                    && (!string.Equals(options.Provider, "ollama", StringComparison.OrdinalIgnoreCase)
                        || (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                            && !string.IsNullOrWhiteSpace(options.Model)))
                    && (string.IsNullOrWhiteSpace(options.Provider)
                        || string.Equals(options.Provider, "none", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(options.Provider, "ollama", StringComparison.OrdinalIgnoreCase)),
                "Embeddings options are invalid.")
            .ValidateOnStart();

        var embeddingProvider = configuration
            .GetSection(EmbeddingOptions.SectionName)
            .GetValue<string>(nameof(EmbeddingOptions.Provider));
        if (string.Equals(embeddingProvider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(ServiceCollectionExtensions.OllamaEmbeddingHttpClientName, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
            services.AddSingleton<IEmbeddingGenerator, OllamaEmbeddingGenerator>();
        }

        return services;
    }
}
