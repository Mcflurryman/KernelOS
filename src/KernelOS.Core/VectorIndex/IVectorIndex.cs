namespace KernelOS.Core.VectorIndex;

public interface IVectorIndex
{
    Task<VectorAddResult> AddAsync(VectorAddRequest request, CancellationToken cancellationToken = default);
    Task<VectorUpdateResult> UpdateAsync(VectorUpdateRequest request, CancellationToken cancellationToken = default);
    Task<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default);
    Task<VectorGetResult> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ContainsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
    Task<VectorQueryResult> QueryAsync(VectorQuery query, CancellationToken cancellationToken = default);
}
