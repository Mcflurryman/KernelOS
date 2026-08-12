using KernelOS.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure;

internal static class PersistenceInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .Validate(options => PersistencePathResolver.IsValidDatabaseFile(options.DatabaseFile), "Persistence:DatabaseFile must be a simple file name.")
            .ValidateOnStart();
        services.AddSingleton<PersistencePathResolver>();
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<ISqliteDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddHostedService<SqliteDatabaseInitializationHostedService>();
        return services;
    }
}
