using System.Text.Json.Serialization;

namespace KernelOS.Infrastructure;

public sealed record OllamaApiRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyCollection<OllamaApiMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream = false);
