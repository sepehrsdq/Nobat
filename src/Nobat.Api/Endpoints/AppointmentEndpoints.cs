using Nobat.Application.Appointments;

namespace Nobat.Api.Endpoints;

/// <summary>The secretary's day view. Requires the panel cookie; the tenant comes from it.</summary>
public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments")
            .WithTags("Appointments")
            .RequireAuthorization();

        // No date means "today in Tehran", which is what the panel opens on.
        group.MapGet("", async (
            DateOnly? date,
            Guid? staffId,
            ListAppointmentsHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(date, staffId, cancellationToken)));

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelAppointmentRequest? request,
            CancelAppointmentHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)));

        group.MapGet("/{id:guid}/slots", async (
            Guid id,
            DateOnly? date,
            GetRescheduleSlotsHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, date, cancellationToken)));

        group.MapPost("/{id:guid}/reschedule", async (
            Guid id,
            RescheduleAppointmentRequest request,
            RescheduleAppointmentHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)));

        return app;
    }
}
