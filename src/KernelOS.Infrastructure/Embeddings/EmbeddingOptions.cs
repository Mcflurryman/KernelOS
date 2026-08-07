namespace KernelOS.Infrastructure.Embeddings;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public string Provider { get; init; } = "none";
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "embeddinggemma";
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxInputCharacters { get; init; } = 8000;
    public int MaxBatchSize { get; init; } = 32;
    public int ExpectedDimensions { get; init; } = 768;
    public bool AllowPartialBatchResults { get; init; }
}
