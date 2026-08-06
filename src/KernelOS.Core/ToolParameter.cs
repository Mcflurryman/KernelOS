namespace KernelOS.Core;

public sealed record ToolParameter(
    string Name,
    string Description,
    string ValueType,
    bool IsRequired);
