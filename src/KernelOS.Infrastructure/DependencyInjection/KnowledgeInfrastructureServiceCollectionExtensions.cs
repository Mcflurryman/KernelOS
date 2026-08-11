using KernelOS.Core.Knowledge;
using KernelOS.Infrastructure.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class KnowledgeInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KnowledgeOptions>()
            .Bind(configuration.GetSection(KnowledgeOptions.SectionName))
            .Validate(
                options => options.MaxItemCharacters > 0
                    && options.ChunkOverlapCharacters >= 0
                    && options.ChunkOverlapCharacters < options.MaxItemCharacters
                    && options.MaxItemsPerDocument > 0,
                "Knowledge options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IKnowledgeBuilder, KnowledgeBuilder>();

        return services;
    }
}
