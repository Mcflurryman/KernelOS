using KernelOS.Core.Memory;
using KernelOS.Infrastructure.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class MemoryInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddMemoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MemoryOptions>()
            .Bind(configuration.GetSection(MemoryOptions.SectionName))
            .Validate(
                options => options.MaxDocuments > 0
                    && options.MaxItemsPerDocument > 0
                    && options.MaxQueryResults > 0,
                "Memory options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IMemoryStore, SqliteMemoryStore>();

        return services;
    }
}
