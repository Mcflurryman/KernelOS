namespace KernelOS.Infrastructure;
public sealed class FilesystemOptions { public const string SectionName = "Filesystem"; public string[] AllowedRoots { get; init; } = []; }
