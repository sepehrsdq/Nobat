using Nobat.Application.Public;

namespace Nobat.Api.Endpoints;

/// <summary>The booking page. No cookie, no tenant header: the slug in the route is the tenant.</summary>
public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public/{slug}")
            .WithTags("Public")
            .AllowAnonymous()
            .RequireRateLimiting("public");

        group.MapGet("", async (
            string slug,
            GetPublicClinicHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(slug, cancellationToken)));

        group.MapGet("/slots", async (
            string slug,
            Guid serviceId,
            Guid staffId,
            DateOnly date,
            GetPublicSlotsHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(slug, serviceId, staffId, date, cancellationToken)));

        return app;
    }
}
