using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Appointments;
using Nobat.Domain.Common;
using Nobat.Domain.Scheduling;

namespace Nobat.Application.Public;

/// <summary>
/// Free slots for one staff member, one service and one local (Tehran) day.
/// The heavy lifting is <see cref="SlotPolicy"/>; this handler only gathers the inputs.
/// </summary>
public sealed class GetPublicSlotsHandler
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextSetter _tenantContext;
    private readonly TimeProvider _clock;

    public GetPublicSlotsHandler(
        IApplicationDbContext db,
        ITenantResolver tenantResolver,
        ITenantContextSetter tenantContext,
        TimeProvider clock)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PublicSlotDto>> HandleAsync(
        string slug,
        Guid serviceId,
        Guid staffId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantResolver.FindBySlugAsync(slug, cancellationToken)
            ?? throw new DomainRuleViolationException("tenant.not_found", "چنین مطبی وجود ندارد.");

        _tenantContext.SetTenant(tenant.Id);

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == serviceId && candidate.IsActive, cancellationToken)
            ?? throw new DomainRuleViolationException("service.not_found", "این خدمت در دسترس نیست.");

        var staffExists = await _db.Staff
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == staffId && candidate.IsActive, cancellationToken);

        if (!staffExists)
        {
            throw new DomainRuleViolationException("staff.not_found", "این پرسنل در دسترس نیست.");
        }

        var dayOfWeek = date.DayOfWeek;

        var rawRanges = await _db.WorkingHours
            .AsNoTracking()
            .Where(hour => hour.StaffId == staffId
                && hour.IsActive
                && hour.DayOfWeek == dayOfWeek)
            .Select(hour => new { hour.StartTime, hour.EndTime })
            .ToListAsync(cancellationToken);

        if (rawRanges.Count == 0)
        {
            return Array.Empty<PublicSlotDto>();
        }

        var ranges = rawRanges
            .Select(hour => new LocalTimeRange(hour.StartTime, hour.EndTime))
            .ToArray();

        var (dayStartUtc, dayEndUtc) = LocalDayBounds(date);

        var rawBusy = await _db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StaffId == staffId
                && appointment.Status == AppointmentStatus.Booked
                && appointment.StartUtc >= dayStartUtc
                && appointment.StartUtc < dayEndUtc)
            .Select(appointment => new { appointment.StartUtc, appointment.EndUtc })
            .ToListAsync(cancellationToken);

        var busy = rawBusy
            .Select(appointment => new UtcInterval(appointment.StartUtc, appointment.EndUtc))
            .ToArray();

        var slots = SlotPolicy.FreeSlots(
            date,
            ranges,
            TimeSpan.FromMinutes(service.DurationMinutes),
            busy,
            _clock.GetUtcNow(),
            ClinicTime.Zone);

        return slots
            .Select(slot => new PublicSlotDto(slot.StartUtc, slot.EndUtc))
            .ToArray();
    }

    private static (DateTimeOffset Start, DateTimeOffset End) LocalDayBounds(DateOnly date)
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
}
