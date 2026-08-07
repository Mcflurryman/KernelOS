namespace KernelOS.Core.Memory;

public interface IMemoryStore
{
    Task<MemoryStoreResult> StoreAsync(MemoryStoreRequest request, CancellationToken cancellationToken = default);
    Task<MemoryUpdateResult> UpdateAsync(MemoryUpdateRequest request, CancellationToken cancellationToken = default);
    Task<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default);
    Task<MemoryGetResult> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<MemoryQueryResult> QueryAsync(MemoryQuery query, CancellationToken cancellationToken = default);
}
