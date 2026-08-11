using KernelOS.Core.Rag;
using KernelOS.Infrastructure.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class RagInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddRagInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(
                options => options.MaxQueryCharacters > 0
                    && options.DefaultTopK > 0
                    && options.MaxTopK >= options.DefaultTopK
                    && options.DefaultContextTokens > 0
                    && options.MaxContextTokens >= options.DefaultContextTokens
                    && !string.IsNullOrWhiteSpace(options.SystemInstruction),
                "Rag options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IRagPromptBuilder, RagPromptBuilder>();
        services.AddSingleton<IRagPipeline, RagPipeline>();

        return services;
    }
}
