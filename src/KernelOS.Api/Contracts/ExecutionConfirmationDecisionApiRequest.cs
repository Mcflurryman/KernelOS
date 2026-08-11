using KernelOS.Core.Execution;

namespace KernelOS.Api.Contracts;

public sealed record ExecutionConfirmationDecisionApiRequest(ExecutionConfirmationDecision? Decision);
