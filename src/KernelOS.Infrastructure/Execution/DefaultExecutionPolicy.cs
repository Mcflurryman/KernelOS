using KernelOS.Core.Execution;

namespace KernelOS.Infrastructure.Execution;

public sealed class DefaultExecutionPolicy : IExecutionPolicy
{
    public ExecutionPolicyDecision Evaluate(ExecutionPolicyRequest request)
    {
        if (request is null || request.PlanId == Guid.Empty || request.TaskId == Guid.Empty || string.IsNullOrWhiteSpace(request.ToolName))
        {
            return new(ExecutionPolicyDecisionType.Deny, ExecutionRiskLevel.Critical, ExecutionPolicyReason.InvalidRequest);
        }

        var metadata = request.ToolMetadata;
        if (metadata is null)
        {
            return new(ExecutionPolicyDecisionType.RequireConfirmation, ExecutionRiskLevel.Medium, ExecutionPolicyReason.UnknownToolRequiresConfirmation);
        }

        if (metadata.IsExplicitlyDenied)
        {
            return new(ExecutionPolicyDecisionType.Deny, ExecutionRiskLevel.Critical, ExecutionPolicyReason.PolicyDenied);
        }

        if (metadata.IsReadOnly && !metadata.HasSideEffects)
        {
            return new(ExecutionPolicyDecisionType.Allow, metadata.RiskLevel, ExecutionPolicyReason.ReadOnlyAllowed);
        }

        return metadata.HasSideEffects
            ? new(ExecutionPolicyDecisionType.RequireConfirmation, metadata.RiskLevel, ExecutionPolicyReason.SideEffectRequiresConfirmation)
            : new(ExecutionPolicyDecisionType.RequireConfirmation, metadata.RiskLevel, ExecutionPolicyReason.UnknownToolRequiresConfirmation);
    }
}
