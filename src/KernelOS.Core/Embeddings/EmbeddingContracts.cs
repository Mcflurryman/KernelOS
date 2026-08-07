namespace KernelOS.Core.Embeddings;

public interface IEmbeddingGenerator
{
    EmbeddingModelInfo ModelInfo { get; }
    Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default);
    Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default);
}
