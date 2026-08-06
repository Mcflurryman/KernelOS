using KernelOS.Core;

namespace KernelOS.Tools;

public sealed class KernelToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IKernelTool> toolsByName;

    public KernelToolRegistry(IEnumerable<IKernelTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var registeredTools = tools.ToList();
        var duplicateName = registeredTools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (string.IsNullOrWhiteSpace(duplicateName))
        {
            toolsByName = registeredTools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
            Tools = registeredTools.AsReadOnly();
            return;
        }

        throw new InvalidOperationException($"A tool named '{duplicateName}' is already registered.");
    }

    public IReadOnlyCollection<IKernelTool> Tools { get; }

    public IKernelTool? GetByName(string name) =>
        !string.IsNullOrWhiteSpace(name) && toolsByName.TryGetValue(name, out var tool) ? tool : null;

    public bool Exists(string name) => GetByName(name) is not null;

    public IReadOnlyCollection<IKernelTool> FindByCategory(string category) =>
        Array.AsReadOnly(Tools.Where(tool => string.Equals(tool.Category, category, StringComparison.OrdinalIgnoreCase)).ToArray());

    public IReadOnlyCollection<IKernelTool> FindByCapability(string capability) =>
        Array.AsReadOnly(Tools.Where(tool => tool.Capabilities.Any(item => string.Equals(item.Name, capability, StringComparison.OrdinalIgnoreCase))).ToArray());
}
