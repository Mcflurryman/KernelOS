namespace KernelOS.Core.Execution;

public enum ExecutionPolicyReason { ReadOnlyAllowed, SideEffectRequiresConfirmation, UnknownToolRequiresConfirmation, PolicyDenied, InvalidRequest }
