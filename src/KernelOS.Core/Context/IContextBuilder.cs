namespace KernelOS.Core.Context;

public interface IContextBuilder
{
    Task<ContextBuildResult> BuildAsync(ContextBuildRequest request, CancellationToken cancellationToken = default);
}
