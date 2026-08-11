using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public sealed record ExecutionPreflightResult(ExecutionGateStatus Status, IReadOnlyDictionary<Guid, ExecutionGateResult> Tasks);
