using KernelOS.Api.Contracts;
using KernelOS.Core.Execution;
using KernelOS.Core.Planning;

namespace KernelOS.Api.EndpointMappings;

public static class ExecutionEndpointMappings
{
    public static WebApplication MapExecutionEndpoints(this WebApplication app)
    {
        app.MapGet("/execution/confirmations/{id:guid}", async (
            Guid id,
            IExecutionConfirmationService confirmations,
            CancellationToken cancellationToken) =>
        {
            var result = await confirmations.GetAsync(id, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapPost("/execution/confirmations/{id:guid}", async (
            Guid id,
            ExecutionConfirmationDecisionApiRequest? request,
            IExecutionConfirmationService confirmations,
            CancellationToken cancellationToken) =>
        {
            if (request?.Decision is null)
            {
                return Results.BadRequest(new { error = "decision is required." });
            }

            var result = await confirmations.DecideAsync(id, request.Decision.Value, cancellationToken);
            return result is null
                ? Results.NotFound()
                : !result.Transitioned
                    ? Results.Conflict(result)
                    : Results.Ok(result);
        });

        app.MapPost("/execution/pending/{id:guid}/execute", async (
            Guid id,
            IExecutionConfirmationService confirmations,
            IPlanExecutor executor,
            CancellationToken cancellationToken) =>
        {
            var pending = await confirmations.TryTakeApprovedExecutionAsync(id, cancellationToken);
            if (pending is null)
            {
                return Results.Conflict(new { error = "The pending execution is not approved or is no longer available." });
            }

            if (pending.ApprovalId is null)
            {
                return Results.Conflict(new { error = "The pending execution has no approval." });
            }

            var result = await executor.ExecuteAsync(
                pending.Plan,
                new Dictionary<Guid, Guid> { [pending.TaskId] = pending.ApprovalId.Value },
                cancellationToken);
            return result.Status switch
            {
                PlannerStatus.Completed => Results.Ok(result),
                PlannerStatus.Denied => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
                PlannerStatus.RequiresConfirmation => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
                PlannerStatus.Cancelled => Results.Json(result, statusCode: 499),
                _ => Results.BadRequest(result)
            };
        });

        return app;
    }
}
