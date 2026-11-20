using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Application.Public;
using Nobat.Domain.Appointments;

namespace Nobat.Application.Appointments;

/// <summary>
/// One Tehran day of appointments for the signed-in clinic. The tenant comes from the cookie and
/// the global query filter; nothing here accepts a tenant id from the caller.
/// </summary>
public sealed class ListAppointmentsHandler
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public ListAppointmentsHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DayAppointmentsDto> HandleAsync(
        DateOnly? date,
        Guid? staffId,
        CancellationToken cancellationToken = default)
    {
        var localDate = date ?? SlotLookup.LocalDateOf(_clock.GetUtcNow());
        var (dayStartUtc, dayEndUtc) = SlotLookup.LocalDayBounds(localDate);

        var query = _db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StartUtc >= dayStartUtc
                && appointment.StartUtc < dayEndUtc);

        if (staffId.HasValue)
        {
            query = query.Where(appointment => appointment.StaffId == staffId.Value);
        }

        var appointments = await query
            .OrderBy(appointment => appointment.StartUtc)
            .ToListAsync(cancellationToken);

        return new DayAppointmentsDto(
            localDate,
            await AppointmentProjection.MapAsync(_db, appointments, cancellationToken));
    }
}
