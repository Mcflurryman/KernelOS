namespace KernelOS.Infrastructure.Embeddings;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public int MaxInputCharacters { get; init; } = 8000;
    public int MaxBatchSize { get; init; } = 32;
    public int ExpectedDimensions { get; init; } = 384;
    public bool AllowPartialBatchResults { get; init; }
}
