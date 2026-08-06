using KernelOS.Core;

namespace KernelOS.Api.Contracts;

public sealed record ChatApiRequest(
    string? Message,
    string? SystemPrompt = null,
    IReadOnlyCollection<ChatMessage>? History = null);
