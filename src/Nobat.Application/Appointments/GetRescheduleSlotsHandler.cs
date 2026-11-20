using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Application.Public;
using Nobat.Domain.Appointments;
using Nobat.Domain.Common;

namespace Nobat.Application.Appointments;

/// <summary>
/// Free starts for moving one appointment. The panel does not have to send the service or the
/// staff: they come from the row itself, so the times offered always match the appointment length.
/// </summary>
public sealed class GetRescheduleSlotsHandler
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetRescheduleSlotsHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<RescheduleSlotsDto> HandleAsync(
        Guid appointmentId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _db.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken)
            ?? throw new DomainRuleViolationException("appointment.not_found", "این نوبت پیدا نشد.");

        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new DomainRuleViolationException(
                "appointment.not_reschedulable",
                "فقط نوبت‌های فعال را می‌توان جابه‌جا کرد.");
        }

        var localDate = date ?? SlotLookup.LocalDateOf(appointment.StartUtc);
        var durationMinutes = (int)(appointment.EndUtc - appointment.StartUtc).TotalMinutes;

        var slots = await SlotLookup.FreeSlotsAsync(
            _db,
            appointment.StaffId,
            durationMinutes,
            localDate,
            _clock.GetUtcNow(),
            cancellationToken);

        return new RescheduleSlotsDto(
            localDate,
            slots.Select(slot => new AppointmentSlotDto(slot.StartUtc, slot.EndUtc)).ToArray());
    }
}
