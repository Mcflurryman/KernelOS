namespace KernelOS.Infrastructure.Context;

public sealed class ContextOptions
{
    public const string SectionName = "Context";
    public int DefaultMaxTokens { get; init; } = 6000;
    public int MaxAllowedTokens { get; init; } = 12000;
    public int DefaultMaxItems { get; init; } = 20;
    public int MaxAllowedItems { get; init; } = 100;
    public float CharactersPerTokenEstimate { get; init; } = 4f;
}
