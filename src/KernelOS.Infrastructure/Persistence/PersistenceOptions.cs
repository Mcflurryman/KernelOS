namespace KernelOS.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";
    public string? DataDirectory { get; init; }
    public string DatabaseFile { get; init; } = "kernelos.db";
}
