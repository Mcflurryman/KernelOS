using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KernelOS.Core.Documents;
using KernelOS.Core.Knowledge;

namespace KernelOS.Infrastructure.Knowledge;

public sealed class KnowledgeBuilder : IKnowledgeBuilder
{
    private static readonly HashSet<string> UnsafePropertyNames = new(StringComparer.OrdinalIgnoreCase) { "internalreference", "path", "fullpath" };

    public Task<KnowledgeBuildResult> BuildAsync(KnowledgeBuildRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new KnowledgeBuildResult(KnowledgeBuildStatus.Cancelled, Error: "Knowledge building was cancelled."));
        if (request.RawDocument is null || !Valid(request.Options)) return Task.FromResult(new KnowledgeBuildResult(KnowledgeBuildStatus.InvalidDocument, Error: "The knowledge build request is invalid."));

        try
        {
            var raw = request.RawDocument;
            var metadata = CreateMetadata(raw);
            var warnings = new List<KnowledgeWarning>();
            var items = new List<KnowledgeItem>();
            var order = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var truncated = false;

            foreach (var section in raw.Sections.OrderBy(section => section.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                truncated |= AddContent(section.Content, Map(section.Kind), Source(raw, section.Id, section.Locator), metadata, request.Options, items, warnings, seen, ref order);
            }

            if (!string.IsNullOrEmpty(raw.TextContent) && !SectionsRepresent(raw))
            {
                truncated |= AddContent(raw.TextContent, KnowledgeItemType.Text, Source(raw, null, null), metadata, request.Options, items, warnings, seen, ref order);
            }

            foreach (var table in raw.Tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = JsonSerializer.Serialize(new { table.Name, table.Headers, Rows = table.Rows.Select(row => new { row.Index, row.Cells }) });
                truncated |= AddContent(content, KnowledgeItemType.Table, Source(raw, table.Id, table.Locator), metadata, request.Options, items, warnings, seen, ref order);
            }

            if (request.Options.IncludeMetadataItems)
            {
                var content = JsonSerializer.Serialize(metadata);
                truncated |= AddContent(content, KnowledgeItemType.Metadata, Source(raw, null, null), metadata, request.Options, items, warnings, seen, ref order);
            }

            return Task.FromResult(Result(raw, metadata, request.Options, items, warnings, truncated));
        }
        catch (OperationCanceledException) { return Task.FromResult(new KnowledgeBuildResult(KnowledgeBuildStatus.Cancelled, Error: "Knowledge building was cancelled.")); }
        catch { return Task.FromResult(new KnowledgeBuildResult(KnowledgeBuildStatus.Failed, Error: "Knowledge building failed.")); }
    }

    private static KnowledgeBuildResult Result(RawDocument raw, KnowledgeMetadata metadata, KnowledgeOptionsSnapshot options, List<KnowledgeItem> items, List<KnowledgeWarning> warnings, bool truncated)
    {
        if (truncated && !warnings.Any(warning => warning.Code == "KNOWLEDGE_TRUNCATED")) warnings.Add(new("KNOWLEDGE_TRUNCATED", "The knowledge document reached the configured item limit."));
        var hash = Hash($"{raw.ContentHash}|{options.MaxItemCharacters}|{options.ChunkOverlapCharacters}|{options.MaxItemsPerDocument}|{options.IncludeMetadataItems}");
        var document = new KnowledgeDocument(Id(hash), raw.Id, raw.Title, items, metadata, warnings, raw.ReadAt, hash);
        return new(truncated ? KnowledgeBuildStatus.PartialSuccess : KnowledgeBuildStatus.Success, document, warnings);
    }

    private static bool AddContent(string content, KnowledgeItemType type, KnowledgeSource source, KnowledgeMetadata metadata, KnowledgeOptionsSnapshot options, List<KnowledgeItem> items, List<KnowledgeWarning> warnings, HashSet<string> seen, ref int order)
    {
        var chunks = Chunks(content, options).ToArray();
        var truncated = items.Count + chunks.Length > options.MaxItemsPerDocument;
        foreach (var (chunk, chunkIndex) in chunks.Select((chunk, index) => (chunk, index)))
        {
            if (items.Count >= options.MaxItemsPerDocument) return true;
            var normalized = Normalize(chunk);
            var chunkSource = chunks.Length == 1 ? source : WithChunkLocator(source, chunkIndex + 1);
            var deduplicationKey = $"{type}|{normalized}|{chunkSource.DocumentId}|{chunkSource.Locator?.SectionId}|{chunkSource.Locator?.Line}|{chunkSource.Locator?.Column}|{chunkSource.Locator?.Row}|{chunkSource.Locator?.JsonPath}|{chunkSource.Locator?.Description}";
            if (!seen.Add(deduplicationKey)) { warnings.Add(new("DUPLICATE_CONTENT_SKIPPED", "An exact duplicate from the same source was skipped.", Locator: chunkSource.Locator)); continue; }
            var hash = Hash(normalized);
            items.Add(new KnowledgeItem(Id($"{chunkSource.DocumentId}|{order}|{type}|{hash}"), type, chunk, order++, chunkSource, metadata, hash));
        }
        return truncated;
    }

    private static IEnumerable<string> Chunks(string content, KnowledgeOptionsSnapshot options)
    {
        if (content.Length <= options.MaxItemCharacters) { yield return content; yield break; }
        var start = 0;
        while (start < content.Length)
        {
            var end = Math.Min(start + options.MaxItemCharacters, content.Length);
            if (end < content.Length)
            {
                var paragraph = content.LastIndexOf("\n\n", end - 1, end - start, StringComparison.Ordinal);
                var line = content.LastIndexOf('\n', end - 1, end - start);
                var preferred = Math.Max(paragraph, line);
                if (preferred > start + options.ChunkOverlapCharacters) end = preferred + (preferred == paragraph ? 2 : 1);
            }
            yield return content[start..end];
            if (end == content.Length) yield break;
            start = Math.Max(end - options.ChunkOverlapCharacters, start + 1);
        }
    }

    private static bool SectionsRepresent(RawDocument raw) => raw.Sections.Count > 0 && Normalize(string.Join("\n", raw.Sections.OrderBy(section => section.Order).Select(section => section.Content))) == Normalize(raw.TextContent!);
    private static bool Valid(KnowledgeOptionsSnapshot options) => options.MaxItemCharacters > 0 && options.ChunkOverlapCharacters >= 0 && options.ChunkOverlapCharacters < options.MaxItemCharacters && options.MaxItemsPerDocument > 0;
    private static KnowledgeItemType Map(DocumentSectionKind kind) => kind switch { DocumentSectionKind.Heading => KnowledgeItemType.Heading, DocumentSectionKind.List => KnowledgeItemType.List, DocumentSectionKind.CodeBlock => KnowledgeItemType.Code, DocumentSectionKind.JsonValue => KnowledgeItemType.JsonValue, _ => KnowledgeItemType.Text };
    private static KnowledgeSource Source(RawDocument raw, string? sectionId, DocumentLocator? locator)
    {
        var safeReference = PublicReference(raw.Source.SafeLogReference, "Document");
        return new(raw.Id, safeReference, PublicReference(raw.Source.DisplayReference, safeReference), new(sectionId, locator?.Line, locator?.Column, locator?.Row, locator?.JsonPath, locator?.Description));
    }
    private static KnowledgeSource WithChunkLocator(KnowledgeSource source, int chunk)
    {
        var locator = source.Locator ?? new KnowledgeLocator();
        var description = string.IsNullOrWhiteSpace(locator.Description) ? $"Chunk {chunk}" : $"{locator.Description}; chunk {chunk}";
        return source with { Locator = locator with { Description = description } };
    }
    private static KnowledgeMetadata CreateMetadata(RawDocument raw) => new(raw.Metadata.MimeType, raw.Format.ToString(), null, raw.Metadata.Properties?.Where(property => !UnsafePropertyNames.Contains(property.Key)).ToDictionary());
    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    private static string PublicReference(string? reference, string fallback) => string.IsNullOrWhiteSpace(reference) || IsAbsoluteReference(reference) ? fallback : reference;
    private static bool IsAbsoluteReference(string reference) => reference.StartsWith('/') || reference.StartsWith('\\') || (reference.Length > 2 && char.IsLetter(reference[0]) && reference[1] == ':' && (reference[2] == '\\' || reference[2] == '/'));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Guid Id(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
}
