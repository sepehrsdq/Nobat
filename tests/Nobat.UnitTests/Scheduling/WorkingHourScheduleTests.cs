using Nobat.Domain.Common;
using Nobat.Domain.Scheduling;

namespace Nobat.UnitTests.Scheduling;

public sealed class WorkingHourScheduleTests
{
    private static LocalTimeRange Range(int startHour, int endHour)
        => new(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    [Fact]
    public void EnsureNoOverlaps_TouchingWindows_AreAllowed()
    {
        var ranges = new[]
        {
            (DayOfWeek.Saturday, Range(9, 12)),
            (DayOfWeek.Saturday, Range(12, 15)),
        };

        Should.NotThrow(() => WorkingHourSchedule.EnsureNoOverlaps(ranges));
    }

    [Fact]
    public void EnsureNoOverlaps_SameDayOverlap_ThrowsWithStableCode()
    {
        var ranges = new[]
        {
            (DayOfWeek.Saturday, Range(9, 12)),
            (DayOfWeek.Saturday, Range(11, 14)),
        };

        var exception = Should.Throw<DomainRuleViolationException>(
            () => WorkingHourSchedule.EnsureNoOverlaps(ranges));

        exception.Code.ShouldBe("working_hour.overlap");
    }

    [Fact]
    public void EnsureNoOverlaps_IdenticalWindows_Throw()
    {
        var ranges = new[]
        {
            (DayOfWeek.Monday, Range(9, 12)),
            (DayOfWeek.Monday, Range(9, 12)),
        };

        Should.Throw<DomainRuleViolationException>(() => WorkingHourSchedule.EnsureNoOverlaps(ranges));
    }

    [Fact]
    public void EnsureNoOverlaps_DifferentDays_AreIndependent()
    {
        var ranges = new[]
        {
            (DayOfWeek.Saturday, Range(9, 12)),
            (DayOfWeek.Sunday, Range(10, 13)),
            (DayOfWeek.Friday, Range(9, 12)),
        };

        Should.NotThrow(() => WorkingHourSchedule.EnsureNoOverlaps(ranges));
    }
}
