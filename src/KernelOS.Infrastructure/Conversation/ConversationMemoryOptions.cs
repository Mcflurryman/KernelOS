namespace KernelOS.Infrastructure.Conversation;

public sealed class ConversationMemoryOptions
{
    public const string SectionName = "ConversationMemory";
    public int MaxMessageCharacters { get; init; } = 16_000;
    public int MaxListPageSize { get; init; } = 100;
    public int MaxMessagesPageSize { get; init; } = 200;
}
