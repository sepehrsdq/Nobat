using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Appointments;
using Nobat.Domain.Common;

namespace Nobat.Application.Appointments;

/// <summary>
/// Cancels without deleting: the row keeps the history, and the filtered unique index lets the
/// freed slot be booked again. Idempotence is deliberately absent — a second cancel is a 409, not a
/// silent success, so an out-of-date screen notices.
/// </summary>
public sealed class CancelAppointmentHandler
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CancelAppointmentHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AppointmentDto> HandleAsync(
        Guid appointmentId,
        CancelAppointmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken)
            ?? throw new DomainRuleViolationException("appointment.not_found", "این نوبت پیدا نشد.");

        appointment.Cancel(_clock.GetUtcNow(), request?.Reason);

        await _db.SaveChangesAsync(cancellationToken);

        return await AppointmentProjection.MapSingleAsync(_db, appointment, cancellationToken);
    }
}
