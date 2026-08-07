using System.Security.Cryptography;
using System.Text;
using KernelOS.Core.Knowledge;
using KernelOS.Core.Memory;

namespace KernelOS.Infrastructure.Memory;

internal static class MemoryDocumentFactory
{
    public static MemoryDocument Create(KnowledgeDocument source, DateTimeOffset now) => Create(source.Id, source, now);

    public static MemoryDocument Create(Guid id, KnowledgeDocument source, DateTimeOffset now)
    {
        var items = source.Items.Select(Map).ToArray();
        var metadata = Copy(source.Metadata);
        var hash = Hash(source.Id, items, metadata);
        return new(id, source.Id, now, now, new(1, now, hash), items, metadata, hash);
    }

    public static MemoryDocument Update(MemoryDocument current, IReadOnlyList<MemoryItem> items, KnowledgeMetadata metadata, DateTimeOffset now)
    {
        var copiedItems = items.Select(Copy).ToArray();
        var copiedMetadata = Copy(metadata);
        var hash = Hash(current.KnowledgeDocumentId, copiedItems, copiedMetadata);
        return current with { UpdatedAt = now, Version = new(current.Version.Number + 1, now, hash), Items = copiedItems, Metadata = copiedMetadata, ContentHash = hash };
    }

    public static MemoryDocument Copy(MemoryDocument document) => document with { Items = document.Items.Select(Copy).ToArray(), Metadata = Copy(document.Metadata), Version = document.Version with { } };

    private static MemoryItem Map(KnowledgeItem item) => new(item.Id, item.Id, item.Type, item.Content, Copy(item.Source), Copy(item.Metadata), item.ContentHash);
    private static MemoryItem Copy(MemoryItem item) => item with { Source = Copy(item.Source), Metadata = Copy(item.Metadata) };
    private static KnowledgeSource Copy(KnowledgeSource source) => source with { Locator = source.Locator is null ? null : source.Locator with { } };
    private static KnowledgeMetadata Copy(KnowledgeMetadata metadata) => metadata with { Properties = metadata.Properties?.ToDictionary(pair => pair.Key, pair => pair.Value) };

    private static string Hash(Guid knowledgeDocumentId, IReadOnlyList<MemoryItem> items, KnowledgeMetadata metadata)
    {
        var value = new StringBuilder(knowledgeDocumentId.ToString("N"));
        foreach (var item in items) value.Append('|').Append(item.KnowledgeItemId.ToString("N")).Append('|').Append(item.Type).Append('|').Append(item.ContentHash).Append('|').Append(item.Content);
        value.Append('|').Append(metadata.MimeType).Append('|').Append(metadata.Format).Append('|').Append(metadata.Language);
        var properties = metadata.Properties is null
            ? Enumerable.Empty<KeyValuePair<string, string>>()
            : metadata.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal);
        foreach (var property in properties) value.Append('|').Append(property.Key).Append('=').Append(property.Value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }
}
