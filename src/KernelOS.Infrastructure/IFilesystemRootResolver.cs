namespace KernelOS.Infrastructure;
public interface IFilesystemRootResolver { IReadOnlyCollection<string> ResolveAllowedRoots(); bool TryResolvePath(string path, out string resolvedPath); }
