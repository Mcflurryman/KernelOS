using KernelOS.Core;

namespace KernelOS.Tools;

public interface IToolRegistry
{
    IReadOnlyCollection<IKernelTool> Tools { get; }

    IKernelTool? GetByName(string name);

    bool Exists(string name);

    IReadOnlyCollection<IKernelTool> FindByCategory(string category);

    IReadOnlyCollection<IKernelTool> FindByCapability(string capability);
}
