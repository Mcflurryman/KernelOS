namespace KernelOS.Core.Filesystem;
public sealed record FilesystemMetadata(string Name, string Path, long Size, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt, string Extension, string Type);
