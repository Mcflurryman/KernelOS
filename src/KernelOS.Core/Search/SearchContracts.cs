namespace KernelOS.Core.Search;

public interface ISearchEngine
{
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
}
