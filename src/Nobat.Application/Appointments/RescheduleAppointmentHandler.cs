using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Application.Public;
using Nobat.Domain.Appointments;
using Nobat.Domain.Common;

namespace Nobat.Application.Appointments;

/// <summary>
/// Moves an existing appointment to another free slot. The appointment keeps its id, patient and
/// service, so the "کد پیگیری" the patient wrote down still works. The new slot goes through the
/// same <see cref="SlotLookup"/> as the public page, and the unique index catches a race.
/// </summary>
public sealed class RescheduleAppointmentHandler
{
    private readonly IApplicationDbContext _db;
    private readonly IDatabaseErrorTranslator _databaseErrors;
    private readonly TimeProvider _clock;

    public RescheduleAppointmentHandler(
        IApplicationDbContext db,
        IDatabaseErrorTranslator databaseErrors,
        TimeProvider clock)
    {
        _db = db;
        _databaseErrors = databaseErrors;
        _clock = clock;
    }

    public async Task<AppointmentDto> HandleAsync(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken)
            ?? throw new DomainRuleViolationException("appointment.not_found", "این نوبت پیدا نشد.");

        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new DomainRuleViolationException(
                "appointment.not_reschedulable",
                "فقط نوبت‌های فعال را می‌توان جابه‌جا کرد.");
        }

        var now = _clock.GetUtcNow();
        var start = request.StartUtc.ToUniversalTime();

        if (start == appointment.StartUtc)
        {
            return await AppointmentProjection.MapSingleAsync(_db, appointment, cancellationToken);
        }

        var durationMinutes = (int)(appointment.EndUtc - appointment.StartUtc).TotalMinutes;

        var freeSlots = await SlotLookup.FreeSlotsAsync(
            _db,
            appointment.StaffId,
            durationMinutes,
            SlotLookup.LocalDateOf(start),
            now,
            cancellationToken);

        if (!freeSlots.Any(slot => slot.StartUtc == start))
        {
            throw new DomainRuleViolationException(
                "appointment.slot_unavailable",
                "این زمان آزاد نیست. زمان دیگری انتخاب کنید.");
        }

        appointment.Reschedule(start, start.AddMinutes(durationMinutes), now);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (_databaseErrors.IsUniqueViolation(exception))
        {
            throw new DomainRuleViolationException(
                "appointment.slot_taken",
                "همین لحظه این زمان پر شد. زمان دیگری انتخاب کنید.");
        }

        return await AppointmentProjection.MapSingleAsync(_db, appointment, cancellationToken);
    }
}
