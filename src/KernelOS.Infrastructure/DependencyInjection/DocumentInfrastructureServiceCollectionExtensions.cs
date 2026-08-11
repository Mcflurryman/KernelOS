using KernelOS.Core.Documents;
using KernelOS.Infrastructure.Documents;
using KernelOS.Infrastructure.Documents.Readers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class DocumentInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddDocumentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DocumentReaderOptions>()
            .Bind(configuration.GetSection(DocumentReaderOptions.SectionName))
            .Validate(
                options => options.MaxFileSizeBytes > 0
                    && options.MaxExtractedCharacters > 0
                    && options.MaxRows > 0
                    && options.MaxColumns > 0
                    && options.TimeoutSeconds > 0,
                "DocumentReaders limits must be greater than zero.")
            .ValidateOnStart();

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
