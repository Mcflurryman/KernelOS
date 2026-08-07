namespace KernelOS.Core.Context;

public interface IContextTokenEstimator
{
    int Estimate(string text);
}
