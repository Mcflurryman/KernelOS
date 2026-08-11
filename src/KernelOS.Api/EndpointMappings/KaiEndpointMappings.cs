using KernelOS.Api.Contracts;
using KernelOS.Core.Kai;

namespace KernelOS.Api.EndpointMappings;

public static class KaiEndpointMappings
{
    public static WebApplication MapKaiEndpoints(this WebApplication app)
    {
        app.MapPost("/kai", async (KaiApiRequest? request, IKaiAgent kai, CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest();
            }

            var result = await kai.HandleAsync(
                new KaiRequest(request.Message, PreferredMode: request.PreferredMode, ToolName: request.ToolName, Arguments: request.Arguments),
                cancellationToken);

            return result.Status switch
            {
                KaiStatus.Completed or KaiStatus.Success or KaiStatus.PartialSuccess => Results.Ok(result),
                KaiStatus.RequiresConfirmation => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
                KaiStatus.Denied => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
                KaiStatus.Cancelled => Results.Json(result, statusCode: 499),
                KaiStatus.InvalidRequest or KaiStatus.PlanningFailed => Results.BadRequest(result),
                _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }
}
