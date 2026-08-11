using KernelOS.Core.Filesystem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class FilesystemInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddFilesystemInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FilesystemOptions>()
            .Bind(configuration.GetSection(FilesystemOptions.SectionName));
        services.AddSingleton<IFilesystemCapability, LocalFilesystemCapability>();
        services.AddSingleton<IFilesystemRootResolver, FilesystemRootResolver>();

        return services;
    }
}
