using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Tools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKernelTools(this IServiceCollection services)
    {
        services.AddSingleton<IKernelTool, EchoTool>();
        services.AddSingleton<IKernelTool, TimeTool>();
        services.AddSingleton<IKernelTool, FilesystemTool>();
        services.AddSingleton<IToolRegistry, KernelToolRegistry>();
        services.AddSingleton<IToolRouter, KernelToolRouter>();

        return services;
    }
}
