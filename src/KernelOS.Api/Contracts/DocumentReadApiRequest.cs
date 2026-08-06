namespace KernelOS.Api.Contracts;

public sealed record DocumentReadApiRequest(string? Path, string? Format = null);
