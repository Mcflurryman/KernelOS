using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string OllamaHttpClientName = "Ollama";
    public const string OllamaEmbeddingHttpClientName = "OllamaEmbeddings";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddChatInfrastructure(configuration)
            .AddExecutionInfrastructure(configuration)
            .AddPlanningInfrastructure()
            .AddFilesystemInfrastructure(configuration)
            .AddDocumentInfrastructure(configuration)
            .AddKnowledgeInfrastructure(configuration)
            .AddPersistenceInfrastructure(configuration)
            .AddMemoryInfrastructure(configuration)
            .AddRetrievalInfrastructure(configuration)
            .AddContextInfrastructure(configuration)
            .AddRagInfrastructure(configuration)
            .AddConversationInfrastructure(configuration)
            .AddKaiInfrastructure(configuration)
            .AddEmbeddingInfrastructure(configuration);

        return services;
    }
}
