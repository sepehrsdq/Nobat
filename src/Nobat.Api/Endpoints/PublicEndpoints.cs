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

        group.MapPost("/otp", async (
            string slug,
            RequestOtpRequest request,
            RequestOtpHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(slug, request, cancellationToken)))
            .RequireRateLimiting("otp");

        group.MapPost("/appointments", async (
            string slug,
            BookAppointmentRequest request,
            BookAppointmentHandler handler,
            CancellationToken cancellationToken) =>
        {
            var booked = await handler.HandleAsync(slug, request, cancellationToken);

            return Results.Json(booked, statusCode: StatusCodes.Status201Created);
        });

        return app;
    }
}
