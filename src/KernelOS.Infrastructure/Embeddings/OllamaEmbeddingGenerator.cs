using System.Net.Http.Json;
using System.Text.Json;
using KernelOS.Core.Embeddings;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Embeddings;

public sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly EmbeddingOptions options;

    public OllamaEmbeddingGenerator(IHttpClientFactory httpClientFactory, IOptions<EmbeddingOptions> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        ModelInfo = new("ollama", this.options.Model, null, this.options.ExpectedDimensions, this.options.MaxInputCharacters, true);
    }

    public EmbeddingModelInfo ModelInfo { get; }

    public async Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default)
    {
        var batch = await GenerateCoreAsync([input], cancellationToken);
        return batch.Status == EmbeddingStatus.Success && batch.Results is { Count: 1 } ? batch.Results[0] : new(batch.Status, Warnings: batch.Warnings, Error: batch.Error);
    }

    public Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default) =>
        request.Inputs is null ? Task.FromResult(new EmbeddingBatchResult(EmbeddingStatus.InvalidInput, Error: "The embedding batch request is invalid.")) : GenerateCoreAsync(request.Inputs, cancellationToken);

    private async Task<EmbeddingBatchResult> GenerateCoreAsync(IReadOnlyList<EmbeddingInput> inputs, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return new(EmbeddingStatus.Cancelled);
        if (inputs.Count == 0 || inputs.Count > options.MaxBatchSize || inputs.Select(input => input.Id).Distinct().Count() != inputs.Count) return new(EmbeddingStatus.InvalidInput, Error: "The embedding batch request is invalid.");
        var prepared = new List<(EmbeddingInput Input, string Text, string Hash)>();
        foreach (var input in inputs)
        {
            if (input.Id == Guid.Empty || input.Text is null) return new(EmbeddingStatus.InvalidInput, Error: "The embedding input is invalid.");
            var text = EmbeddingText.Normalize(input.Text);
            if (text.Length == 0) return new(EmbeddingStatus.InvalidInput, Error: "The embedding input is invalid.");
            if (text.Length > options.MaxInputCharacters) return new(EmbeddingStatus.TooLarge, Error: "The embedding input exceeds the configured limit.");
            prepared.Add((input, text, input.ContentHash ?? EmbeddingText.Hash(text)));
        }

        try
        {
            using var response = await httpClientFactory.CreateClient(ServiceCollectionExtensions.OllamaEmbeddingHttpClientName)
                .PostAsJsonAsync("api/embed", new OllamaEmbeddingRequest(options.Model, prepared.Count == 1 ? prepared[0].Text : prepared.Select(value => value.Text).ToArray()), cancellationToken);
            if (!response.IsSuccessStatusCode) return new(EmbeddingStatus.Failed, Error: "Embedding provider is unavailable.");
            var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
            if (payload?.Embeddings is null || payload.Embeddings.Count != prepared.Count) return new(EmbeddingStatus.Failed, Error: "Embedding provider returned an invalid response.");
            var results = new List<EmbeddingResult>();
            for (var index = 0; index < prepared.Count; index++)
            {
                var values = payload.Embeddings[index];
                if (values is null || values.Count != options.ExpectedDimensions || values.Count == 0 || values.Any(value => !float.IsFinite(value))) return new(EmbeddingStatus.Failed, Error: "Embedding provider returned incompatible dimensions.");
                var value = prepared[index];
                results.Add(new(EmbeddingStatus.Success, new EmbeddingVector(value.Input.Id, values, options.ExpectedDimensions, options.Model, null, value.Hash, DateTimeOffset.UtcNow)));
            }
            return new(EmbeddingStatus.Success, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(EmbeddingStatus.Cancelled); }
        catch (OperationCanceledException) { return new(EmbeddingStatus.Failed, Error: "Embedding provider is unavailable."); }
        catch (HttpRequestException) { return new(EmbeddingStatus.Failed, Error: "Embedding provider is unavailable."); }
        catch (JsonException) { return new(EmbeddingStatus.Failed, Error: "Embedding provider returned an invalid response."); }
    }
}
