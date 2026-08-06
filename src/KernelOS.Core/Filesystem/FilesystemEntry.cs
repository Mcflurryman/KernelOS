namespace KernelOS.Core.Filesystem;
public sealed record FilesystemEntry(string Name, string Path, string Type, long Size, DateTimeOffset ModifiedAt);
