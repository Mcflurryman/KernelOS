namespace KernelOS.Core;

public interface IChatModel
{
    Task<ChatResponse> SendAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
