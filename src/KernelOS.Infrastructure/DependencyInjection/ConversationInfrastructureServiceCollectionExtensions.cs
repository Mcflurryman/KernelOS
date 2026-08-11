using KernelOS.Core.Conversation;
using KernelOS.Infrastructure.Conversation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class ConversationInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddConversationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ConversationContextOptions>()
            .Bind(configuration.GetSection(ConversationContextOptions.SectionName))
            .Validate(
                options => options.DefaultMaxTokens > 0
                    && options.MaxAllowedTokens >= options.DefaultMaxTokens
                    && options.DefaultMaxTurns > 0
                    && options.MaxAllowedTurns >= options.DefaultMaxTurns
                    && options.CharactersPerTokenEstimate > 0
                    && float.IsFinite(options.CharactersPerTokenEstimate),
                "ConversationContext options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IConversationContextBuilder, ConversationContextBuilder>();

        return services;
    }
}
