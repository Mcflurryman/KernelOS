namespace KernelOS.Core.HybridSearch;
public interface IHybridSearchEngine { Task<HybridSearchResponse> SearchAsync(HybridSearchRequest request, CancellationToken cancellationToken = default); }
