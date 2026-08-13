using System.Security.Cryptography;
using System.Text;
using KernelOS.Core.Embeddings;
using KernelOS.Core.Memory;
using KernelOS.Core.VectorIndex;

namespace KernelOS.Infrastructure.SemanticIndex;

internal static class SemanticVectorRecords
{
    internal static Guid CreateRecordId(Guid inputId, VectorFamilyKey family)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(inputId.ToString("N"), ":", Encode(family.Provider), ":", Encode(family.Model), ":", family.Version is null ? "N" : "V" + Encode(family.Version), ":", family.Dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        return new Guid(bytes.AsSpan(0, 16));
    }

    internal static bool TryCorrelate(IReadOnlyList<MemoryItem> items, IReadOnlyList<EmbeddingResult> results, EmbeddingModelInfo model, out Dictionary<Guid, EmbeddingVector> vectors)
    {
        vectors = [];
        if (items.Count != results.Count) return false;
        var expected = items.ToDictionary(item => item.Id);
        foreach (var result in results)
        {
            var vector = result.Vector;
            if (result.Status != EmbeddingStatus.Success || vector is null || !vector.IsValid() || !expected.TryGetValue(vector.InputId, out var item)
                || !string.Equals(vector.ContentHash, item.ContentHash, StringComparison.Ordinal) || !string.Equals(vector.Model, model.Model, StringComparison.Ordinal)
                || !string.Equals(vector.ModelVersion, model.Version, StringComparison.Ordinal) || vector.Dimensions != model.Dimensions || !vectors.TryAdd(vector.InputId, vector)) return false;
        }
        return vectors.Count == expected.Count;
    }

    internal static VectorRecord Create(MemoryDocument document, MemoryItem item, EmbeddingVector vector, EmbeddingModelInfo model, VectorFamilyKey family, DateTimeOffset now) =>
        new(CreateRecordId(item.Id, family), model.Provider, vector, document.Id, document.KnowledgeDocumentId, item.Id, item.KnowledgeItemId, item.ContentHash, now, now);

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
