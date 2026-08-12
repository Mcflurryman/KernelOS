using KernelOS.Core.VectorReindex;
using KernelOS.Infrastructure.VectorReindex;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class VectorReindexInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddVectorReindexInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IVectorReindexService, MemoryVectorReindexService>();
        return services;
    }
}
