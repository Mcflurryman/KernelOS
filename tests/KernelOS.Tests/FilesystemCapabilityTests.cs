using System.Text.Json;
using KernelOS.Core.Filesystem;
using KernelOS.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KernelOS.Tests;

public sealed class FilesystemCapabilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KernelOS-Fs-{Guid.NewGuid()}");
    private readonly LocalFilesystemCapability capability;

    public FilesystemCapabilityTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        Directory.CreateDirectory(Path.Combine(root, "empty"));
        File.WriteAllText(Path.Combine(root, "sample.cs"), "class Sample { }");
        File.WriteAllText(Path.Combine(root, "sub", "nested.txt"), "nested");
        File.WriteAllText(Path.Combine(root, "\u00E1.txt"), "unicode");

        capability = new LocalFilesystemCapability(CreateResolver());
    }

    [Fact]
    public void RootAliasesResolveToAbsolutePathsAndDuplicateRootsAreRemoved()
    {
        var resolver = CreateResolver();

        Assert.True(resolver.TryResolvePath("Workspace", out var workspace));
        Assert.True(resolver.TryResolvePath("Desktop", out var desktop));
        Assert.True(resolver.TryResolvePath("Documents", out var documents));
        Assert.True(Path.IsPathFullyQualified(workspace));
        Assert.True(Path.IsPathFullyQualified(desktop));
        Assert.True(Path.IsPathFullyQualified(documents));
        var duplicateOnlyResolver = new FilesystemRootResolver(
            Options.Create(new FilesystemOptions { AllowedRoots = [root, root] }),
            new TestHostEnvironment(root));
        Assert.Single(duplicateOnlyResolver.ResolveAllowedRoots());
    }

    [Theory]
    [InlineData("Unknown/a")]
    [InlineData("relative/a")]
    [InlineData("Workspace/../../Windows")]
    public async Task UnknownOrEscapingRelativePathsAreRejected(string path)
    {
        var result = await RunAsync("resolve", path);

        Assert.False(result.Success);
        Assert.Equal("unauthorized", result.Error);
    }

    [Fact]
    public async Task AbsolutePathsRespectRootBoundariesIncludingSimilarPrefixes()
    {
        var outside = await RunAsync("metadata", root + "Other");
        var inside = await RunAsync("exists", Path.Combine(root, "sample.cs"));

        Assert.Equal("unauthorized", outside.Error);
        Assert.True(inside.Success);
    }

    [Fact]
    public async Task SearchRespectsPatternsRecursionLimitsAndStableOrdering()
    {
        var sourceFiles = await RunAsync("search", root, ("pattern", "*.cs"));
        var shallow = await RunAsync("search", root, ("pattern", "*.txt"));
        var recursive = await RunAsync("search", root, ("pattern", "*.txt"), ("recursive", "true"));
        var limited = await RunAsync("list", root, ("maxResults", "1"));
        var listed = Entries(await RunAsync("list", root));

        Assert.Contains(Entries(sourceFiles), entry => entry.Name == "sample.cs");
        Assert.DoesNotContain(Entries(shallow), entry => entry.Name == "nested.txt");
        Assert.Contains(Entries(recursive), entry => entry.Name == "nested.txt");
        Assert.Single(Entries(limited));
        Assert.Equal(
            listed.Select(entry => entry.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            listed.Select(entry => entry.Path));
    }

    [Fact]
    public async Task ExistsMetadataResolveAndEmptyDirectoryReturnExpectedResults()
    {
        var fileExists = Json(await RunAsync("exists", Path.Combine(root, "sample.cs")));
        var directoryExists = Json(await RunAsync("exists", root));
        var missingExists = Json(await RunAsync("exists", Path.Combine(root, "none")));
        var metadata = Assert.IsType<FilesystemMetadata>(
            (await RunAsync("metadata", Path.Combine(root, "sample.cs"))).Data);
        var directoryMetadata = Assert.IsType<FilesystemMetadata>((await RunAsync("metadata", root)).Data);
        var emptyDirectory = await RunAsync("list", Path.Combine(root, "empty"));
        var resolved = await RunAsync("resolve", Path.Combine(root, "sub", "..", "sample.cs"));

        Assert.True(fileExists.GetProperty("exists").GetBoolean());
        Assert.True(directoryExists.GetProperty("exists").GetBoolean());
        Assert.False(missingExists.GetProperty("exists").GetBoolean());
        Assert.Equal("sample.cs", metadata.Name);
        Assert.Equal(".cs", metadata.Extension);
        Assert.Equal("file", metadata.Type);
        Assert.Equal("directory", directoryMetadata.Type);
        Assert.Empty(Entries(emptyDirectory));
        Assert.True(resolved.Success);
    }

    [Fact]
    public async Task NotFoundUnicodeAndCancellationAreControlled()
    {
        var missing = await RunAsync("metadata", Path.Combine(root, "none"));
        var missingDirectory = await RunAsync("list", Path.Combine(root, "none"));
        var unicode = await RunAsync("metadata", Path.Combine(root, "\u00E1.txt"));
        var invalidPattern = await RunAsync("search", root, ("pattern", "\0"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await capability.ExecuteAsync("exists", Arguments(root), cancellation.Token);

        Assert.Equal("not_found", missing.Error);
        Assert.Equal("not_found", missingDirectory.Error);
        Assert.True(unicode.Success);
        Assert.Equal("invalid_arguments", invalidPattern.Error);
        Assert.Equal("cancelled", cancelled.Error);
    }

    private FilesystemRootResolver CreateResolver() => new FilesystemRootResolver(
        Options.Create(new FilesystemOptions { AllowedRoots = [root, "Desktop", "Documents", "Workspace"] }),
        new TestHostEnvironment(root));

    private Task<FilesystemOperationResult> RunAsync(
        string operation,
        string path,
        params (string Name, string Value)[] extras) =>
        capability.ExecuteAsync(operation, Arguments(path, extras));

    private static Dictionary<string, JsonElement> Arguments(
        string path,
        params (string Name, string Value)[] extras)
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["path"] = JsonSerializer.SerializeToElement(path)
        };

        foreach (var (name, value) in extras)
        {
            arguments[name] = name switch
            {
                "recursive" => JsonSerializer.SerializeToElement(bool.Parse(value)),
                "maxResults" => JsonSerializer.SerializeToElement(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)),
                _ => JsonSerializer.SerializeToElement(value)
            };
        }

        return arguments;
    }

    private static JsonElement Json(FilesystemOperationResult result) =>
        JsonSerializer.SerializeToElement(result.Data);

    private static IReadOnlyCollection<FilesystemEntry> Entries(FilesystemOperationResult result) =>
        Assert.IsType<FilesystemSearchResult>(result.Data).Entries;

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "KernelOS.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
