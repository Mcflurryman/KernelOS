using KernelOS.Core.Memory;
using KernelOS.Core.SemanticIndex;
using KernelOS.Infrastructure.Memory;
using KernelOS.Infrastructure.SemanticIndex;
using Microsoft.Extensions.Hosting;
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
        services.AddSingleton<SemanticIndexCoordinator>();
        services.AddSingleton<ISemanticIndexCoordinator>(provider => provider.GetRequiredService<SemanticIndexCoordinator>());
        services.AddOptions<SemanticIndexMaintenanceOptions>()
            .Bind(configuration.GetSection(SemanticIndexMaintenanceOptions.SectionName))
            .Validate(options => options.QueueCapacity > 0, "Semantic index maintenance options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<SemanticMutationBuffer>();
        services.AddSingleton<IMemoryMutationObserver>(provider => provider.GetRequiredService<SemanticMutationBuffer>());
        services.AddHostedService<SemanticIndexMaintenanceWorker>();
        services.AddSingleton<SqliteMemoryStore>();
        services.AddSingleton<IMemoryStore>(provider => provider.GetRequiredService<SqliteMemoryStore>());
        services.AddSingleton<IMemorySnapshotProvider>(provider => provider.GetRequiredService<SqliteMemoryStore>());

        return services;
    }
}
