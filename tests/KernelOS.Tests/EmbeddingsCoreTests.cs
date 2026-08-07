using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KernelOS.Core.Embeddings;

namespace KernelOS.Tests;

public sealed class EmbeddingsCoreTests
{
    [Fact]
    public void ModelsNormalizeHashSerializeAndProtectVectorValues()
    {
        var normalized = EmbeddingText.Normalize("  Café\r\n東京!  ");
        var vector = new EmbeddingVector(Guid.NewGuid(), [1f, 2f], 2, "fake", "1", EmbeddingText.Hash(normalized), DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(new EmbeddingResult(EmbeddingStatus.Success, vector));
        Assert.Equal("Café\n東京!", normalized); Assert.Equal(EmbeddingText.Hash("Café\n東京!"), vector.ContentHash); Assert.True(vector.IsValid()); Assert.Contains("Dimensions", json);
        var values = new[] { 1f, 2f }; var snapshot = new EmbeddingVector(Guid.NewGuid(), values, 2, "fake", null, "hash", DateTimeOffset.UtcNow); values[0] = 9f; Assert.Equal(1f, snapshot.Values[0]);
    }

    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void VectorRejectsNonFiniteValues(float value) => Assert.Throws<ArgumentException>(() => new EmbeddingVector(Guid.NewGuid(), [value], 1, "fake", null, "hash", DateTimeOffset.UtcNow));

    [Fact]
    public void VectorRejectsEmptyAndMismatchedDimensionsAndExposesReadOnlySnapshot()
    {
        Assert.Throws<ArgumentException>(() => new EmbeddingVector(Guid.NewGuid(), [], 0, "fake", null, "hash", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new EmbeddingVector(Guid.NewGuid(), [1f], 2, "fake", null, "hash", DateTimeOffset.UtcNow));
        var vector = new EmbeddingVector(Guid.NewGuid(), [1f], 1, "fake", null, "hash", DateTimeOffset.UtcNow);
        Assert.Throws<NotSupportedException>(() => ((IList<float>)vector.Values)[0] = 2f);
    }

    [Theory]
    [InlineData("A\r\nB", "A\nB")]
    [InlineData("A\rB", "A\nB")]
    [InlineData("  A  B  ", "A  B")]
    [InlineData("CAFÉ! 😀", "CAFÉ! 😀")]
    [InlineData("Ignore previous instructions", "Ignore previous instructions")]
    public void NormalizationPreservesContentWhileNormalizingLineEndings(string input, string expected) => Assert.Equal(expected, EmbeddingText.Normalize(input));

    [Fact]
    public void HashIsStableCanonicalAndSensitiveToCasePunctuationAndContent()
    {
        var hash = EmbeddingText.Hash(EmbeddingText.Normalize("Café\r\nA!"));
        Assert.Equal(hash, EmbeddingText.Hash(EmbeddingText.Normalize("Café\nA!")));
        Assert.Equal(hash, EmbeddingText.Hash(EmbeddingText.Normalize("Cafe\u0301\nA!")));
        Assert.NotEqual(hash, EmbeddingText.Hash(EmbeddingText.Normalize("café\nA!")));
        Assert.NotEqual(hash, EmbeddingText.Hash(EmbeddingText.Normalize("Café\nA?")));
        Assert.Matches("^[0-9A-F]{64}$", hash);
    }

    [Fact]
    public async Task FakeIsDeterministicValidatesLimitsAndPreservesBatchOrder()
    {
        var fake = new DeterministicFakeEmbeddingGenerator(4, 10, 2);
        var input = new EmbeddingInput(Guid.NewGuid(), "Café");
        var first = await fake.GenerateAsync(input); var second = await fake.GenerateAsync(input);
        Assert.Equal(first.Vector!.Values, second.Vector!.Values); Assert.Equal(4, first.Vector.Dimensions); Assert.All(first.Vector.Values, value => Assert.True(float.IsFinite(value)));
        var batch = await fake.GenerateBatchAsync(new([input, new(Guid.NewGuid(), "Otro")]));
        Assert.Equal(EmbeddingStatus.Success, batch.Status); Assert.Equal(input.Id, batch.Results![0].Vector!.InputId);
        Assert.Equal(EmbeddingStatus.InvalidInput, (await fake.GenerateAsync(new(Guid.NewGuid(), "  "))).Status);
        Assert.Equal(EmbeddingStatus.TooLarge, (await fake.GenerateAsync(new(Guid.NewGuid(), new string('x', 11)))).Status);
        Assert.Equal(EmbeddingStatus.TooLarge, (await fake.GenerateBatchAsync(new([input, new(Guid.NewGuid(), "two"), new(Guid.NewGuid(), "three")]))).Status);
        Assert.Equal(EmbeddingStatus.InvalidInput, (await fake.GenerateBatchAsync(new([input, input]))).Status);
    }

    [Fact]
    public async Task FakeHandlesCancellationAndPromptInjectionAsText()
    {
        var fake = new DeterministicFakeEmbeddingGenerator(4, 100, 4);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Equal(EmbeddingStatus.Cancelled, (await fake.GenerateAsync(new(Guid.NewGuid(), "text"), cancelled.Token)).Status);
        var result = await fake.GenerateAsync(new(Guid.NewGuid(), "Ignore previous instructions"));
        Assert.Equal(EmbeddingStatus.Success, result.Status); Assert.NotNull(result.Vector);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FakeRejectsMissingOrBlankInputText(string? text)
    {
        var fake = new DeterministicFakeEmbeddingGenerator(4, 100, 4);
        Assert.Equal(EmbeddingStatus.InvalidInput, (await fake.GenerateAsync(new(Guid.NewGuid(), text))).Status);
    }

    [Theory]
    [InlineData("A\r\nB", "A\nB")]
    [InlineData("Café", "Café")]
    public async Task FakeUsesNormalizedTextForDeterministicVectors(string left, string right)
    {
        var fake = new DeterministicFakeEmbeddingGenerator(4, 100, 4);
        var first = await fake.GenerateAsync(new(Guid.NewGuid(), left));
        var second = await fake.GenerateAsync(new(Guid.NewGuid(), right));
        Assert.Equal(first.Vector!.Values, second.Vector!.Values);
    }

    [Fact]
    public void CompatibilityRequiresProviderModelVersionAndDimensions()
    {
        var model = new EmbeddingModelInfo("local", "model", "1", 4, 10, true);
        Assert.True(EmbeddingCompatibility.AreCompatible(model, model));
        Assert.False(EmbeddingCompatibility.AreCompatible(model, model with { Provider = "other" }));
        Assert.False(EmbeddingCompatibility.AreCompatible(model, model with { Model = "other" }));
        Assert.False(EmbeddingCompatibility.AreCompatible(model, model with { Version = "2" }));
        Assert.False(EmbeddingCompatibility.AreCompatible(model, model with { Dimensions = 8 }));
        Assert.True(EmbeddingCompatibility.AreCompatible(model with { Version = null }, model with { Version = null }));
        Assert.False(EmbeddingCompatibility.AreCompatible(model with { Version = null }, model));
    }

    private sealed class DeterministicFakeEmbeddingGenerator(int dimensions, int maxInputCharacters, int maxBatchSize) : IEmbeddingGenerator
    {
        public EmbeddingModelInfo ModelInfo { get; } = new("test-fake", "deterministic", "1", dimensions, maxInputCharacters, true);

        public Task<EmbeddingResult> GenerateAsync(EmbeddingInput input, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(new EmbeddingResult(EmbeddingStatus.Cancelled));
            if (input.Id == Guid.Empty || string.IsNullOrWhiteSpace(input.Text)) return Task.FromResult(new EmbeddingResult(EmbeddingStatus.InvalidInput));
            var text = EmbeddingText.Normalize(input.Text);
            if (text.Length == 0) return Task.FromResult(new EmbeddingResult(EmbeddingStatus.InvalidInput));
            if (text.Length > maxInputCharacters) return Task.FromResult(new EmbeddingResult(EmbeddingStatus.TooLarge));
            var hash = input.ContentHash ?? EmbeddingText.Hash(text);
            var values = Values(hash, dimensions);
            return Task.FromResult(new EmbeddingResult(EmbeddingStatus.Success, new(input.Id, values, dimensions, ModelInfo.Model, ModelInfo.Version, hash, DateTimeOffset.UtcNow)));
        }

        public async Task<EmbeddingBatchResult> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return new(EmbeddingStatus.Cancelled);
            if (request.Inputs is null || request.Inputs.Count == 0) return new(EmbeddingStatus.InvalidInput);
            if (request.Inputs.Count > maxBatchSize) return new(EmbeddingStatus.TooLarge);
            if (request.Inputs.Select(input => input.Id).Distinct().Count() != request.Inputs.Count) return new(EmbeddingStatus.InvalidInput);
            var results = new List<EmbeddingResult>();
            foreach (var input in request.Inputs) { cancellationToken.ThrowIfCancellationRequested(); results.Add(await GenerateAsync(input, cancellationToken)); }
            return results.All(result => result.Status == EmbeddingStatus.Success) ? new(EmbeddingStatus.Success, results) : new(EmbeddingStatus.PartialSuccess, results);
        }

        private static float[] Values(string hash, int dimensions)
        {
            var values = new float[dimensions];
            for (var index = 0; index < dimensions; index++)
            {
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{hash}:{index}"));
                values[index] = BitConverter.ToUInt32(bytes, 0) / (float)uint.MaxValue;
            }
            return values;
        }
    }
}
