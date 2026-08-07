namespace KernelOS.Core.SemanticSearch;

public interface ISemanticSearchEngine
{
    Task<SemanticSearchResponse> SearchAsync(SemanticSearchRequest request, CancellationToken cancellationToken = default);
}
