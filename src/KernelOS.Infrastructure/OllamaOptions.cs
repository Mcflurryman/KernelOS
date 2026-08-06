namespace KernelOS.Infrastructure;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "qwen3:8b";

    public int TimeoutSeconds { get; init; } = 120;

    public string SystemPrompt { get; init; } = "Eres Kai, el asistente personal local de KernelOS. Responde en español de forma clara y útil.";
}
