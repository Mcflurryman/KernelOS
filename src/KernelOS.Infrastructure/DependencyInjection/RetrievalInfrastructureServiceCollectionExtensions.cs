using KernelOS.Core.HybridSearch;
using KernelOS.Core.Search;
using KernelOS.Core.SemanticSearch;
using KernelOS.Core.VectorIndex;
using KernelOS.Infrastructure.HybridSearch;
using KernelOS.Infrastructure.Search;
using KernelOS.Infrastructure.SemanticSearch;
using KernelOS.Infrastructure.VectorIndex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KernelOS.Infrastructure;

internal static class RetrievalInfrastructureServiceCollectionExtensions
{
    internal static IServiceCollection AddRetrievalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName))
            .Validate(
                options => options.MaxQueryLength > 0
                    && options.MaxTokens > 0
                    && options.MaxCandidateDocuments > 0
                    && options.MaxResults > 0,
                "Search options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<ISearchEngine, MemorySearchEngine>();

        services.AddOptions<VectorIndexOptions>()
            .Bind(configuration.GetSection(VectorIndexOptions.SectionName))
            .Validate(
                options => options.MaxRecords > 0
                    && options.MaxQueryResults > 0
                    && options.MaxMetadataEntries > 0,
                "VectorIndex options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();

        services.AddOptions<SemanticSearchOptions>()
            .Bind(configuration.GetSection(SemanticSearchOptions.SectionName))
            .Validate(
                options => options.MaxCandidates > 0
                    && options.DefaultTopK > 0
                    && options.MaxTopK >= options.DefaultTopK
                    && options.CandidatePageSize > 0,
                "SemanticSearch options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<ISemanticSearchEngine, SemanticSearchEngine>();

        services.AddOptions<HybridSearchOptions>()
            .Bind(configuration.GetSection(HybridSearchOptions.SectionName))
            .Validate(
                options => options.LexicalWeight >= 0
                    && options.SemanticWeight >= 0
                    && options.LexicalWeight + options.SemanticWeight > 0
                    && options.DefaultTopK > 0
                    && options.MaxTopK >= options.DefaultTopK
                    && options.CandidateMultiplier > 0,
                "HybridSearch options are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IHybridSearchEngine, HybridSearchEngine>();

        return services;
    }
}
