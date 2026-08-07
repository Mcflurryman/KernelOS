namespace KernelOS.Core.Rag;

public interface IRagPipeline
{
    Task<RagResponse> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default);
}
