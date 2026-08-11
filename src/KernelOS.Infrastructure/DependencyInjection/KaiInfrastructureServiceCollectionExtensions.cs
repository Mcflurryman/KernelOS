using KernelOS.Core.Kai;
using KernelOS.Infrastructure.Kai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class KaiInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddKaiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KaiOptions>()
            .Bind(configuration.GetSection(KaiOptions.SectionName))
            .Validate(options => options.MaxMessageCharacters > 0, "Kai options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IKaiIntentRouter, DeterministicKaiIntentRouter>();
        services.AddSingleton<IKaiAgent, KaiAgent>();

        return services;
    }
}
