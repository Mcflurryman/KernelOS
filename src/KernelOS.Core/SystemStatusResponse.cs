namespace KernelOS.Core;

public sealed record SystemStatusResponse(
    string Status,
    string Application,
    string Assistant,
    string Version);
