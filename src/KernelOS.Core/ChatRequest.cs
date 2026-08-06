namespace KernelOS.Core;

public sealed record ChatRequest
{
    public ChatRequest(
        string message,
        string? systemPrompt = null,
        IReadOnlyCollection<ChatMessage>? history = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        Message = message;
        SystemPrompt = systemPrompt;
        History = history;
    }

    public string Message { get; init; }

    public string? SystemPrompt { get; init; }

    public IReadOnlyCollection<ChatMessage>? History { get; init; }
}
