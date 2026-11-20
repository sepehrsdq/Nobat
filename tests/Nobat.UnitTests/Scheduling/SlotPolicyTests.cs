using Nobat.Domain.Scheduling;

namespace Nobat.UnitTests.Scheduling;

public sealed class SlotPolicyTests
{
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    private static readonly DateOnly Tuesday = new(2026, 3, 10);
    private static readonly DateTimeOffset LongBefore = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ThirtyMinutes = TimeSpan.FromMinutes(30);

    private static readonly LocalTimeRange Morning = new(new TimeOnly(9, 0), new TimeOnly(12, 0));

    [Fact]
    public void FreeSlots_ThirtyMinuteServiceAcrossThreeHours_ReturnsElevenSlots()
    {
        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], ThirtyMinutes, [], LongBefore, Tehran);

        slots.Count.ShouldBe(11);
        slots[0].StartUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 5, 30, 0, TimeSpan.Zero));
        slots[0].EndUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero));
        slots[^1].StartUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FreeSlots_IsOrderedAndUsesTheFifteenMinuteGrid()
    {
        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], ThirtyMinutes, [], LongBefore, Tehran);

        slots.ShouldBe(slots.OrderBy(slot => slot.StartUtc).ToArray());
        slots.Zip(slots.Skip(1)).ShouldAllBe(pair => pair.Second.StartUtc - pair.First.StartUtc == TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void FreeSlots_ServiceLongerThanWindow_ReturnsEmpty()
    {
        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], TimeSpan.FromHours(4), [], LongBefore, Tehran);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public void FreeSlots_BookedInterval_ExcludesOnlyOverlappingStarts()
    {
        var booked = new UtcInterval(
            new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 10, 6, 30, 0, TimeSpan.Zero));

        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], ThirtyMinutes, [booked], LongBefore, Tehran);

        // 09:00-09:30 touches the booking and stays free; 09:15, 09:30 and 09:45 are removed.
        slots.Count.ShouldBe(8);
        slots.ShouldNotContain(slot => slot.StartUtc == new DateTimeOffset(2026, 3, 10, 5, 45, 0, TimeSpan.Zero));
        slots.ShouldContain(slot => slot.StartUtc == new DateTimeOffset(2026, 3, 10, 5, 30, 0, TimeSpan.Zero));
        slots.ShouldContain(slot => slot.StartUtc == new DateTimeOffset(2026, 3, 10, 6, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FreeSlots_StartsInsideTheLeadTime_AreExcluded()
    {
        // 05:15Z is 08:45 in Tehran; with a 30-minute lead time nothing before 09:15 local is bookable.
        var now = new DateTimeOffset(2026, 3, 10, 5, 15, 0, TimeSpan.Zero);

        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], ThirtyMinutes, [], now, Tehran);

        slots.Count.ShouldBe(10);
        slots[0].StartUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 5, 45, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FreeSlots_PastDate_ReturnsEmpty()
    {
        var slots = SlotPolicy.FreeSlots(new DateOnly(2026, 2, 25), [Morning], ThirtyMinutes, [], LongBefore, Tehran);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public void FreeSlots_Today_IsAllowed()
    {
        var now = new DateTimeOffset(2026, 3, 10, 3, 0, 0, TimeSpan.Zero);

        var slots = SlotPolicy.FreeSlots(Tuesday, [Morning], ThirtyMinutes, [], now, Tehran);

        slots.ShouldNotBeEmpty();
    }

    [Fact]
    public void FreeSlots_BeyondMaxAdvanceDays_ReturnsEmpty()
    {
        var tooFar = Tuesday.AddDays(SlotPolicy.MaxAdvanceDays + 1);

        var slots = SlotPolicy.FreeSlots(tooFar, [Morning], ThirtyMinutes, [], LongBefore, Tehran);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public void FreeSlots_OverlappingWindows_AreMergedWithoutDuplicates()
    {
        var ranges = new[]
        {
            new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(11, 0)),
            new LocalTimeRange(new TimeOnly(10, 0), new TimeOnly(12, 0)),
        };

        var slots = SlotPolicy.FreeSlots(Tuesday, ranges, ThirtyMinutes, [], LongBefore, Tehran);

        slots.Count.ShouldBe(11);
        slots.Select(slot => slot.StartUtc).Distinct().Count().ShouldBe(slots.Count);
    }

    [Fact]
    public void FreeSlots_AdjacentWindows_AllowASlotThatCrossesTheBoundary()
    {
        var ranges = new[]
        {
            new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0)),
            new LocalTimeRange(new TimeOnly(10, 0), new TimeOnly(11, 0)),
        };

        var slots = SlotPolicy.FreeSlots(Tuesday, ranges, ThirtyMinutes, [], LongBefore, Tehran);

        slots.Count.ShouldBe(7);
        slots[^1].EndUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 7, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FreeSlots_NoWorkingRanges_ReturnsEmpty()
    {
        var slots = SlotPolicy.FreeSlots(Tuesday, [], ThirtyMinutes, [], LongBefore, Tehran);

        slots.ShouldBeEmpty();
    }

    [Fact]
    public void FreeSlots_ZeroDuration_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => SlotPolicy.FreeSlots(Tuesday, [Morning], TimeSpan.Zero, [], LongBefore, Tehran));
    }

    [Fact]
    public void IsBooked_TouchingBoundaries_IsNotAnOverlap()
    {
        var booked = new UtcInterval(
            new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 10, 6, 30, 0, TimeSpan.Zero));

        var before = new UtcInterval(
            new DateTimeOffset(2026, 3, 10, 5, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 10, 6, 0, 0, TimeSpan.Zero));

        var after = new UtcInterval(
            new DateTimeOffset(2026, 3, 10, 6, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 10, 7, 0, 0, TimeSpan.Zero));

        SlotPolicy.IsBooked(before, [booked]).ShouldBeFalse();
        SlotPolicy.IsBooked(after, [booked]).ShouldBeFalse();
        SlotPolicy.IsBooked(booked, [booked]).ShouldBeTrue();
    }

    [Fact]
    public void MergeWindows_ReversedRange_Throws()
    {
        var broken = new LocalTimeRange(new TimeOnly(12, 0), new TimeOnly(9, 0));

        Should.Throw<ArgumentException>(() => SlotPolicy.MergeWindows([broken]));
    }
}
