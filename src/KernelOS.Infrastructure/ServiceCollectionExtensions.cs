using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Core.Documents;
using KernelOS.Infrastructure.Documents;
using KernelOS.Infrastructure.Documents.Readers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string OllamaHttpClientName = "Ollama";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "Ollama:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Ollama:Model is required.")
            .Validate(options => options.TimeoutSeconds > 0, "Ollama:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient(OllamaHttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton<IChatModel, OllamaChatModel>();
        services.AddSingleton<IOllamaHealthCheck, OllamaHealthCheck>();
        services.AddSingleton<IPlanner, KernelPlanner>();
        services.AddOptions<FilesystemOptions>().Bind(configuration.GetSection(FilesystemOptions.SectionName));
        services.AddSingleton<KernelOS.Core.Filesystem.IFilesystemCapability, LocalFilesystemCapability>();
        services.AddSingleton<IFilesystemRootResolver, FilesystemRootResolver>();
        services.AddOptions<DocumentReaderOptions>().Bind(configuration.GetSection(DocumentReaderOptions.SectionName)).Validate(o => o.MaxFileSizeBytes > 0 && o.MaxExtractedCharacters > 0 && o.MaxRows > 0 && o.MaxColumns > 0 && o.TimeoutSeconds > 0, "DocumentReaders limits must be greater than zero.").ValidateOnStart();
        services.AddSingleton<IDocumentReader, TxtDocumentReader>();
        services.AddSingleton<IDocumentReader, MarkdownDocumentReader>();
        services.AddSingleton<IDocumentReader, JsonDocumentReader>();
        services.AddSingleton<IDocumentReader, CsvDocumentReader>();
        services.AddSingleton<IDocumentReaderRegistry, DocumentReaderRegistry>();
        services.AddSingleton<IDocumentReaderRouter, DocumentReaderRouter>();
        services.AddSingleton<IDocumentReadService, DocumentReadService>();

        return services;
    }
}
