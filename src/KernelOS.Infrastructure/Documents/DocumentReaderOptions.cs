namespace KernelOS.Infrastructure.Documents;

public sealed class DocumentReaderOptions
{
    public const string SectionName = "DocumentReaders";
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxExtractedCharacters { get; init; } = 1_000_000;
    public int MaxRows { get; init; } = 10_000;
    public int MaxColumns { get; init; } = 200;
    public int TimeoutSeconds { get; init; } = 30;
    public bool AllowPartialResults { get; init; } = true;
}
