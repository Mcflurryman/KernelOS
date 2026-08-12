using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Persistence;

public sealed class PersistencePathResolver(IOptions<PersistenceOptions> options)
{
    private static readonly char[] DirectorySeparators = ['/', '\\'];

    public string DataDirectory { get; } = ResolveDirectory(options.Value.DataDirectory);
    public string DatabasePath { get; } = ResolveDatabasePath(ResolveDirectory(options.Value.DataDirectory), options.Value.DatabaseFile);

    public static bool IsValidDatabaseFile(string? file) =>
        !string.IsNullOrWhiteSpace(file)
        && !Path.IsPathRooted(file)
        && !file.Contains("..", StringComparison.Ordinal)
        && file.IndexOfAny(DirectorySeparators) < 0
        && file.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string ResolveDirectory(string? configured) => string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KernelOS")
        : Path.GetFullPath(configured);

    private static string ResolveDatabasePath(string directory, string file)
    {
        if (!IsValidDatabaseFile(file)) throw new OptionsValidationException(PersistenceOptions.SectionName, typeof(PersistenceOptions), ["Persistence:DatabaseFile must be a simple file name."]);
        var path = Path.GetFullPath(Path.Combine(directory, file));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new OptionsValidationException(PersistenceOptions.SectionName, typeof(PersistenceOptions), ["Persistence:DatabaseFile must stay within DataDirectory."]);
        return path;
    }
}
