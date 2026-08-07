namespace KernelOS.Infrastructure.Embeddings;

internal sealed record OllamaEmbeddingRequest(string Model, object Input);
internal sealed record OllamaEmbeddingResponse(string? Model, IReadOnlyList<IReadOnlyList<float>>? Embeddings);
