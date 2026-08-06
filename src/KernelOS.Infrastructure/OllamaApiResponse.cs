using System.Text.Json.Serialization;

namespace KernelOS.Infrastructure;

public sealed record OllamaApiResponse(
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("message")] OllamaApiMessage? Message);
