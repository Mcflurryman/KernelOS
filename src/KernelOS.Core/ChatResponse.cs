namespace KernelOS.Core;

public sealed record ChatResponse(
    string Message,
    string Model,
    long DurationMilliseconds,
    bool Success,
    string? Error);
