using System.Text.Json.Serialization;

namespace KernelOS.Infrastructure;

public sealed record OllamaApiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
