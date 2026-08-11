using KernelOS.Core.Context;
using KernelOS.Infrastructure.Context;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class ContextInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddContextInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ContextOptions>()
            .Bind(configuration.GetSection(ContextOptions.SectionName))
            .Validate(
                options => options.DefaultMaxTokens > 0
                    && options.MaxAllowedTokens >= options.DefaultMaxTokens
                    && options.DefaultMaxItems > 0
                    && options.MaxAllowedItems >= options.DefaultMaxItems
                    && float.IsFinite(options.CharactersPerTokenEstimate)
                    && options.CharactersPerTokenEstimate > 0,
                "Context options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IContextTokenEstimator, CharacterRatioTokenEstimator>();
        services.AddSingleton<IContextBuilder, ContextBuilder>();

        return services;
    }
}
