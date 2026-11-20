using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;
using Nobat.Domain.Scheduling;
using WorkingHourEntity = Nobat.Domain.Scheduling.WorkingHour;

namespace Nobat.Application.WorkingHours;

public sealed class GetWorkingHoursHandler
{
    private readonly IApplicationDbContext _db;

    public GetWorkingHoursHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<WorkingHoursOfStaff> HandleAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        await StaffLookup.EnsureExistsAsync(_db, staffId, cancellationToken);

        var hours = await _db.WorkingHours
            .AsNoTracking()
            .Where(hour => hour.StaffId == staffId && hour.IsActive)
            .ToListAsync(cancellationToken);

        return WorkingHoursOfStaff.From(hours);
    }
}

internal static class StaffLookup
{
    public static async Task EnsureExistsAsync(
        IApplicationDbContext db,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Staff.AnyAsync(staff => staff.Id == staffId, cancellationToken);

        if (!exists)
        {
            throw new DomainRuleViolationException("staff.not_found", "پرسنل پیدا نشد.");
        }
    }
}

/// <summary>
/// Replaces the whole weekly schedule of one staff member in a single save.
/// Delete and insert travel together, so a rejected range never leaves the schedule half-written.
/// </summary>
public sealed class ReplaceWorkingHoursHandler
{
    private readonly IApplicationDbContext _db;

    public ReplaceWorkingHoursHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<WorkingHoursOfStaff> HandleAsync(
        Guid staffId,
        ReplaceWorkingHoursRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await StaffLookup.EnsureExistsAsync(_db, staffId, cancellationToken);

        var inputs = request.Ranges ?? Array.Empty<WorkingHourInput>();

        if (inputs.Count > WorkingHourSchedule.MaxRangesPerStaff)
        {
            throw new DomainRuleViolationException(
                "working_hour.too_many",
                "تعداد بازه‌های ساعت کاری بیش از حد مجاز است.");
        }

        var created = inputs
            .Select(input => WorkingHourEntity.Create(
                Guid.Empty,
                staffId,
                input.DayOfWeek,
                input.StartTime,
                input.EndTime))
            .ToArray();

        WorkingHourSchedule.EnsureNoOverlaps(created.Select(hour => (hour.DayOfWeek, hour.ToLocalRange())));

        var existing = await _db.WorkingHours
            .Where(hour => hour.StaffId == staffId)
            .ToListAsync(cancellationToken);

        _db.WorkingHours.RemoveRange(existing);
        _db.WorkingHours.AddRange(created);

        await _db.SaveChangesAsync(cancellationToken);

        return WorkingHoursOfStaff.From(created);
    }
}
