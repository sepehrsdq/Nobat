using Nobat.Domain.Common;

namespace Nobat.Domain.Scheduling;

/// <summary>Weekly schedule rules that involve more than one window at a time.</summary>
public static class WorkingHourSchedule
{
    public const int MaxRangesPerStaff = 50;

    /// <summary>
    /// Two windows on the same weekday may touch but must not overlap.
    /// Overlap would make slot generation ambiguous and is rejected outright.
    /// </summary>
    public static void EnsureNoOverlaps(IEnumerable<(DayOfWeek Day, LocalTimeRange Range)> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        foreach (var day in ranges.GroupBy(item => item.Day))
        {
            var ordered = day.Select(item => item.Range).OrderBy(range => range.Start).ToArray();

            for (var i = 1; i < ordered.Length; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    throw new DomainRuleViolationException(
                        "working_hour.overlap",
                        "بازه‌های ساعت کاری یک روز نباید هم‌پوشانی داشته باشند.");
                }
            }
        }
    }
}
