namespace KernelOS.Core.VectorReindex;

public enum VectorReindexStatus { Success, NoMemory, AlreadyRunning, Cancelled, Failed }

public sealed record VectorReindexResult(
    VectorReindexStatus Status,
    string? Provider = null,
    string? Model = null,
    string? Version = null,
    int? Dimensions = null,
    int TotalDocuments = 0,
    int TotalItems = 0,
    int ProcessedItems = 0,
    int IndexedItems = 0,
    int FailedItems = 0,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CapturedAt = null,
    TimeSpan? Duration = null,
    bool Published = false,
    string? ErrorCode = null);
