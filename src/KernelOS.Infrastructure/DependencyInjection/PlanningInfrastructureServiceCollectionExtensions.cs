using KernelOS.Core.Planning;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class PlanningInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddPlanningInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPlanBuilder, PlanBuilder>();
        services.AddSingleton<IPlanExecutor, PlanExecutor>();
        services.AddSingleton<IPlanner, KernelPlanner>();

        return services;
    }
}
