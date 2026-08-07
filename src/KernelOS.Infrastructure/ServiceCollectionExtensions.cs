using KernelOS.Core;
using KernelOS.Core.Planning;
using KernelOS.Core.Documents;
using KernelOS.Infrastructure.Documents;
using KernelOS.Infrastructure.Documents.Readers;
using KernelOS.Core.Knowledge;
using KernelOS.Infrastructure.Knowledge;
using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Memory;
using KernelOS.Core.Search;
using KernelOS.Infrastructure.Search;
using KernelOS.Infrastructure.Embeddings;
using KernelOS.Core.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string OllamaHttpClientName = "Ollama";
    public const string OllamaEmbeddingHttpClientName = "OllamaEmbeddings";

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
        services.AddOptions<KnowledgeOptions>().Bind(configuration.GetSection(KnowledgeOptions.SectionName)).Validate(o => o.MaxItemCharacters > 0 && o.ChunkOverlapCharacters >= 0 && o.ChunkOverlapCharacters < o.MaxItemCharacters && o.MaxItemsPerDocument > 0, "Knowledge options are invalid.").ValidateOnStart();
        services.AddSingleton<IKnowledgeBuilder, KnowledgeBuilder>();
        services.AddOptions<MemoryOptions>().Bind(configuration.GetSection(MemoryOptions.SectionName)).Validate(o => o.MaxDocuments > 0 && o.MaxItemsPerDocument > 0 && o.MaxQueryResults > 0, "Memory options are invalid.").ValidateOnStart();
        services.AddSingleton<IMemoryStore, InMemoryMemoryStore>();
        services.AddOptions<SearchOptions>().Bind(configuration.GetSection(SearchOptions.SectionName)).Validate(o => o.MaxQueryLength > 0 && o.MaxTokens > 0 && o.MaxCandidateDocuments > 0 && o.MaxResults > 0, "Search options are invalid.").ValidateOnStart();
        services.AddSingleton<ISearchEngine, MemorySearchEngine>();
        services.AddOptions<EmbeddingOptions>().Bind(configuration.GetSection(EmbeddingOptions.SectionName)).Validate(o => o.MaxInputCharacters > 0 && o.MaxBatchSize > 0 && o.ExpectedDimensions > 0 && o.TimeoutSeconds > 0 && (!string.Equals(o.Provider, "ollama", StringComparison.OrdinalIgnoreCase) || (Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(o.Model))) && (string.IsNullOrWhiteSpace(o.Provider) || string.Equals(o.Provider, "none", StringComparison.OrdinalIgnoreCase) || string.Equals(o.Provider, "ollama", StringComparison.OrdinalIgnoreCase)), "Embeddings options are invalid.").ValidateOnStart();
        var embeddingProvider = configuration.GetSection(EmbeddingOptions.SectionName).GetValue<string>(nameof(EmbeddingOptions.Provider));
        if (string.Equals(embeddingProvider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(OllamaEmbeddingHttpClientName, (serviceProvider, client) =>
            {
                var embeddingOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbeddingOptions>>().Value;
                client.BaseAddress = new Uri(embeddingOptions.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(embeddingOptions.TimeoutSeconds);
            });
            services.AddSingleton<IEmbeddingGenerator, OllamaEmbeddingGenerator>();
        }

        return services;
    }
}
