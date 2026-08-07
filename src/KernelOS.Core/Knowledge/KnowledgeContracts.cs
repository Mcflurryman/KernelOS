namespace KernelOS.Core.Knowledge;

public interface IKnowledgeBuilder
{
    Task<KnowledgeBuildResult> BuildAsync(KnowledgeBuildRequest request, CancellationToken cancellationToken = default);
}
