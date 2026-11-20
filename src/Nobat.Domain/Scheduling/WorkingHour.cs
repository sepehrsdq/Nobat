using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Domain.Scheduling;

/// <summary>
/// A recurring weekly availability window for one staff member.
/// Times are wall-clock times in the clinic's time zone, never UTC.
/// </summary>
public sealed class WorkingHour : ITenantEntity
{
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromMinutes(15);

    private WorkingHour()
    {
    }

    private WorkingHour(Guid tenantId, Guid staffId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        StaffId = staffId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public Guid StaffId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public bool IsActive { get; private set; }

    public static WorkingHour Create(Guid tenantId, Guid staffId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        EnsureWindowIsValid(startTime, endTime);

        return new WorkingHour(tenantId, staffId, dayOfWeek, startTime, endTime);
    }

    public LocalTimeRange ToLocalRange() => new(StartTime, EndTime);

    public void ChangeWindow(TimeOnly startTime, TimeOnly endTime)
    {
        EnsureWindowIsValid(startTime, endTime);
        StartTime = startTime;
        EndTime = endTime;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void EnsureWindowIsValid(TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new DomainRuleViolationException(
                "working_hour.range_invalid",
                "پایان ساعت کاری باید بعد از شروع آن باشد.");
        }

        if (endTime - startTime < MinimumWindow)
        {
            throw new DomainRuleViolationException(
                "working_hour.too_short",
                "هر بازهٔ کاری باید حداقل ۱۵ دقیقه باشد.");
        }
    }
}
