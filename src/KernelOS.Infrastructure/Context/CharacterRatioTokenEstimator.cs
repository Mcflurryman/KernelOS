using KernelOS.Core.Context;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Context;

public sealed class CharacterRatioTokenEstimator : IContextTokenEstimator
{
    private readonly float charactersPerToken;

    public CharacterRatioTokenEstimator(IOptions<ContextOptions> options) => charactersPerToken = options.Value.CharactersPerTokenEstimate;

    public int Estimate(string text) => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / charactersPerToken);
}
