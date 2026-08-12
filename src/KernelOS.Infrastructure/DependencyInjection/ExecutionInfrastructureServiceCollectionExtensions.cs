using KernelOS.Core.Audit;
using KernelOS.Core.Execution;
using KernelOS.Infrastructure.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class ExecutionInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddExecutionInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutionPolicyOptions>()
            .Bind(configuration.GetSection(ExecutionPolicyOptions.SectionName))
            .Validate(
                options => options.ApprovalTtlMinutes > 0,
                "ExecutionPolicy:ApprovalTtlMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<ExecutionAuditOptions>()
            .Bind(configuration.GetSection(ExecutionAuditOptions.SectionName))
            .Validate(options => options.MaxEvents > 0, "ExecutionAudit:MaxEvents must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IExecutionPolicy, DefaultExecutionPolicy>();
        services.AddSingleton<IExecutionApprovalStore, InMemoryExecutionApprovalStore>();
        services.AddSingleton<IExecutionPendingStore, InMemoryExecutionPendingStore>();
        services.AddSingleton<IExecutionConfirmationService, ExecutionConfirmationService>();
        services.AddSingleton<IExecutionGate, ExecutionGate>();
        services.AddSingleton<IExecutionPreflight, ExecutionPreflight>();
        services.AddSingleton<IExecutionAuditTrail, InMemoryExecutionAuditTrail>();
        services.AddSingleton<IExecutionAuditWriter, SafeExecutionAuditWriter>();

        return services;
    }
}
