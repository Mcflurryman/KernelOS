namespace KernelOS.Tools;

public interface IKernelTool
{
    string Name { get; }

    string Description { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
