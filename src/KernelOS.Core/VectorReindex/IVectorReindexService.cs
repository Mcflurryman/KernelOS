namespace KernelOS.Core.VectorReindex;

public interface IVectorReindexService
{
    Task<VectorReindexResult> ReindexAsync(CancellationToken cancellationToken = default);
}
