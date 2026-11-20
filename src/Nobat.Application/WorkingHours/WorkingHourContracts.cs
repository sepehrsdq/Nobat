using Nobat.Domain.Scheduling;

namespace Nobat.Application.WorkingHours;

public sealed record WorkingHourDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record WorkingHourInput(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record ReplaceWorkingHoursRequest(IReadOnlyList<WorkingHourInput> Ranges);

public sealed record WorkingHoursOfStaff(IReadOnlyList<WorkingHourDto> Ranges)
{
    public static WorkingHoursOfStaff From(IEnumerable<WorkingHour> hours) => new(
        hours
            .OrderBy(hour => PersianWeekOrder(hour.DayOfWeek))
            .ThenBy(hour => hour.StartTime)
            .Select(hour => new WorkingHourDto(hour.DayOfWeek, hour.StartTime, hour.EndTime))
            .ToArray());

    /// <summary>Saturday first, as Iranians read a week.</summary>
    private static int PersianWeekOrder(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => 0,
        DayOfWeek.Sunday => 1,
        DayOfWeek.Monday => 2,
        DayOfWeek.Tuesday => 3,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 5,
        _ => 6,
    };
}
