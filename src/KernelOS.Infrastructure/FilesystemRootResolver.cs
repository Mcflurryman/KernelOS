using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure;

public sealed class FilesystemRootResolver : IFilesystemRootResolver
{
    private readonly IOptions<FilesystemOptions> options;
    private readonly IHostEnvironment environment;
    private readonly Func<Environment.SpecialFolder, string> getSpecialFolderPath;

    public FilesystemRootResolver(
        IOptions<FilesystemOptions> options,
        IHostEnvironment environment,
        Func<Environment.SpecialFolder, string>? getSpecialFolderPath = null)
    {
        this.options = options;
        this.environment = environment;
        this.getSpecialFolderPath = getSpecialFolderPath ?? Environment.GetFolderPath;
    }

    public IReadOnlyCollection<string> ResolveAllowedRoots() =>
        options.Value.AllowedRoots
            .Select(ResolveRoot)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool TryResolvePath(string path, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (Path.IsPathFullyQualified(path))
            {
                var absolutePath = Path.GetFullPath(path);
                var authorizedRoot = ResolveAllowedRoots()
                    .FirstOrDefault(root => IsWithin(absolutePath, root));

                if (authorizedRoot is null)
                {
                    return false;
                }

                resolvedPath = absolutePath;
                return true;
            }

            var normalizedInput = path.Replace('/', Path.DirectorySeparatorChar);
            var separatorIndex = normalizedInput.IndexOf(Path.DirectorySeparatorChar);
            var alias = separatorIndex < 0 ? normalizedInput : normalizedInput[..separatorIndex];
            var relativePath = separatorIndex < 0 ? string.Empty : normalizedInput[(separatorIndex + 1)..];
            var root = ResolveRoot(alias);

            if (root is null || !ResolveAllowedRoots().Any(allowedRoot =>
                    string.Equals(allowedRoot, root, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!IsWithin(candidate, root))
            {
                return false;
            }

            resolvedPath = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private string? ResolveRoot(string value) => value switch
    {
        "Desktop" => ResolveSpecialFolder(Environment.SpecialFolder.DesktopDirectory),
        "Documents" => ResolveSpecialFolder(Environment.SpecialFolder.MyDocuments),
        "Workspace" => NormalizeRoot(FindWorkspaceRoot()),
        _ when Path.IsPathFullyQualified(value) => NormalizeRoot(value),
        _ => null
    };

    private string? ResolveSpecialFolder(Environment.SpecialFolder specialFolder) =>
        NormalizeRoot(getSpecialFolderPath(specialFolder));

    private static string? NormalizeRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            return null;
        }

        try
        {
            var absolutePath = Path.GetFullPath(value);
            var filesystemRoot = Path.GetPathRoot(absolutePath);
            if (string.IsNullOrWhiteSpace(filesystemRoot)
                || string.Equals(
                    Path.TrimEndingDirectorySeparator(absolutePath),
                    Path.TrimEndingDirectorySeparator(filesystemRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return absolutePath;
        }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (PathTooLongException) { return null; }
    }

    private string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(Path.GetFullPath(environment.ContentRootPath));

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KernelOS.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(environment.ContentRootPath);
    }

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
