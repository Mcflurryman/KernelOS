namespace KernelOS.Core.Filesystem;
public sealed record FilesystemSearchRequest(string Path, string? Pattern = null, string? Extension = null, bool Recursive = false, int MaxResults = 100);
