using System.Security.Cryptography;
using System.Text;

namespace KernelOS.Core.Embeddings;

public enum EmbeddingStatus { Success, PartialSuccess, InvalidInput, TooLarge, Unsupported, Cancelled, Failed }

public sealed record EmbeddingInput(Guid Id, string? Text, string? ContentHash = null, IReadOnlyDictionary<string, string>? Metadata = null);
public sealed record EmbeddingModelInfo(string Provider, string Model, string? Version, int Dimensions, int? MaxInputCharacters, bool SupportsBatching);
public sealed record EmbeddingWarning(string Code, string Message);
public sealed record EmbeddingOptionsSnapshot(int MaxInputCharacters, int MaxBatchSize, int ExpectedDimensions, bool AllowPartialBatchResults);
public sealed record EmbeddingBatchRequest(IReadOnlyList<EmbeddingInput>? Inputs);
public sealed record EmbeddingResult(EmbeddingStatus Status, EmbeddingVector? Vector = null, IReadOnlyList<EmbeddingWarning>? Warnings = null, string? Error = null);
public sealed record EmbeddingBatchResult(EmbeddingStatus Status, IReadOnlyList<EmbeddingResult>? Results = null, IReadOnlyList<EmbeddingWarning>? Warnings = null, string? Error = null);

public sealed class EmbeddingVector
{
    public Guid InputId { get; }
    public IReadOnlyList<float> Values { get; }
    public int Dimensions { get; }
    public string Model { get; }
    public string? ModelVersion { get; }
    public string ContentHash { get; }
    public DateTimeOffset GeneratedAt { get; }

    public EmbeddingVector(Guid inputId, IEnumerable<float>? values, int dimensions, string model, string? modelVersion, string contentHash, DateTimeOffset generatedAt)
    {
        var copy = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
        if (dimensions <= 0 || copy.Length == 0 || copy.Length != dimensions || copy.Any(value => !float.IsFinite(value))) throw new ArgumentException("Embedding vector values must be finite and match dimensions.", nameof(values));
        InputId = inputId; Values = Array.AsReadOnly(copy); Dimensions = dimensions; Model = model; ModelVersion = modelVersion; ContentHash = contentHash; GeneratedAt = generatedAt;
    }

    public bool IsValid() => Dimensions > 0 && Values.Count == Dimensions && Values.Count > 0 && Values.All(float.IsFinite);
}

public static class EmbeddingText
{
    public static string Normalize(string text) => text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    public static string Hash(string normalizedText) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
}

public static class EmbeddingCompatibility
{
    public static bool AreCompatible(EmbeddingModelInfo left, EmbeddingModelInfo right) =>
        string.Equals(left.Provider, right.Provider, StringComparison.Ordinal)
        && string.Equals(left.Model, right.Model, StringComparison.Ordinal)
        && string.Equals(left.Version, right.Version, StringComparison.Ordinal)
        && left.Dimensions == right.Dimensions;
}
