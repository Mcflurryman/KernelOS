using System.Text.Json;
using KernelOS.Api.Contracts;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;

namespace KernelOS.Api.EndpointMappings;

public static class PlannerEndpointMappings
{
    public static WebApplication MapPlannerEndpoints(this WebApplication app)
    {
        app.MapPost("/planner/execute", async (
            PlannerExecuteApiRequest? request,
            IPlanner planner,
            IPlanExecutor executor,
            IExecutionConfirmationService confirmations,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Goal) || string.IsNullOrWhiteSpace(request.Tool))
            {
                return Results.BadRequest(new { error = "goal and tool are required." });
            }

            var metadata = new Dictionary<string, JsonElement>
            {
                ["tool"] = JsonSerializer.SerializeToElement(request.Tool),
                ["arguments"] = JsonSerializer.SerializeToElement(request.Arguments ?? new Dictionary<string, JsonElement>())
            };
            var planningResult = await planner.PlanAsync(
                new Goal(Guid.NewGuid(), request.Goal, DateTimeOffset.UtcNow, 0, metadata),
                cancellationToken);

            if (planningResult.Status != PlannerStatus.Planned || planningResult.Plan is null)
            {
                return planningResult.Status == PlannerStatus.Cancelled
                    ? Results.Json(planningResult, statusCode: 499)
                    : Results.BadRequest(planningResult);
            }

            var result = await executor.ExecuteAsync(planningResult.Plan, request.ApprovalIds, cancellationToken);
            if (result.Status == PlannerStatus.RequiresConfirmation)
            {
                var pending = await confirmations.CreatePendingAsync(
                    planningResult.Plan,
                    planningResult.Plan.Tasks.Single().Id,
                    cancellationToken);
                return pending.Confirmation is null
                    ? Results.Json(result, statusCode: StatusCodes.Status409Conflict)
                    : Results.Json(
                        new { pendingExecutionId = pending.Confirmation.PendingExecutionId, confirmation = pending.Confirmation },
                        statusCode: StatusCodes.Status409Conflict);
            }

            return result.Status switch
            {
                PlannerStatus.Completed => Results.Ok(result),
                PlannerStatus.Cancelled => Results.Json(result, statusCode: 499),
                PlannerStatus.Denied => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(result)
            };
        });

        return app;
    }
}
