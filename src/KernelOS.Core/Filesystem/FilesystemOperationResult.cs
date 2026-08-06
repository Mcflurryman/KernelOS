namespace KernelOS.Core.Filesystem;
public sealed record FilesystemOperationResult(bool Success, string? Error = null, object? Data = null);
