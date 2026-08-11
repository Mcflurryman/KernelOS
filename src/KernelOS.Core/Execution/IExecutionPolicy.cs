namespace KernelOS.Core.Execution;

public interface IExecutionPolicy
{
    ExecutionPolicyDecision Evaluate(ExecutionPolicyRequest request);
}
