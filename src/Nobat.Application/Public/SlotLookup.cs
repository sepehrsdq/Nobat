using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Appointments;
using Nobat.Domain.Scheduling;

namespace Nobat.Application.Public;

/// <summary>
/// Gathers what <see cref="SlotPolicy"/> needs and nothing more. Both the public slot list and the
/// booking handler go through here, so "is this slot free" has exactly one answer.
/// </summary>
internal static class SlotLookup
{
    public static async Task<IReadOnlyList<Slot>> FreeSlotsAsync(
        IApplicationDbContext db,
        Guid staffId,
        int durationMinutes,
        DateOnly localDate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var ranges = await db.WorkingHours
            .AsNoTracking()
            .Where(hour => hour.StaffId == staffId
                && hour.IsActive
                && hour.DayOfWeek == localDate.DayOfWeek)
            .Select(hour => new { hour.StartTime, hour.EndTime })
            .ToListAsync(cancellationToken);

        if (ranges.Count == 0)
        {
            return Array.Empty<Slot>();
        }

        var (dayStartUtc, dayEndUtc) = LocalDayBounds(localDate);

        var busy = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StaffId == staffId
                && appointment.Status == AppointmentStatus.Booked
                && appointment.StartUtc >= dayStartUtc
                && appointment.StartUtc < dayEndUtc)
            .Select(appointment => new { appointment.StartUtc, appointment.EndUtc })
            .ToListAsync(cancellationToken);

        return SlotPolicy.FreeSlots(
            localDate,
            ranges.Select(hour => new LocalTimeRange(hour.StartTime, hour.EndTime)).ToArray(),
            TimeSpan.FromMinutes(durationMinutes),
            busy.Select(appointment => new UtcInterval(appointment.StartUtc, appointment.EndUtc)).ToArray(),
            nowUtc,
            ClinicTime.Zone);
    }

    public static (DateTimeOffset Start, DateTimeOffset End) LocalDayBounds(DateOnly date)
    {
        var start = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                ClinicTime.Zone),
            TimeSpan.Zero);

        var end = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                ClinicTime.Zone),
            TimeSpan.Zero);

        return (start, end);
    }

    public static DateOnly LocalDateOf(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant.ToUniversalTime(), ClinicTime.Zone).DateTime);
}
