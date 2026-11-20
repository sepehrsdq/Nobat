using Nobat.Application.Personnel;

namespace Nobat.Api.Endpoints;

public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization();

        group.MapGet("", async (ListStaffHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)));

        group.MapPost("", async (
            CreateStaffRequest request,
            CreateStaffHandler handler,
            CancellationToken cancellationToken) =>
        {
            var staff = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/staff/{staff.Id}", staff);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateStaffRequest request,
            UpdateStaffHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)));

        return app;
    }
}
