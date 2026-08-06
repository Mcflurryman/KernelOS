namespace KernelOS.Core.Filesystem;
public interface IFilesystemCapability { Task<FilesystemOperationResult> ExecuteAsync(string operation, IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments, CancellationToken cancellationToken = default); }
