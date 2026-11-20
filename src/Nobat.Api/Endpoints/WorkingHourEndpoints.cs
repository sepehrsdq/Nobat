using Nobat.Application.WorkingHours;

namespace Nobat.Api.Endpoints;

public static class WorkingHourEndpoints
{
    public static IEndpointRouteBuilder MapWorkingHourEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff/{staffId:guid}/working-hours")
            .WithTags("Working hours")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid staffId,
            GetWorkingHoursHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(staffId, cancellationToken)));

        group.MapPut("", async (
            Guid staffId,
            ReplaceWorkingHoursRequest request,
            ReplaceWorkingHoursHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(staffId, request, cancellationToken)));

        return app;
    }
}
