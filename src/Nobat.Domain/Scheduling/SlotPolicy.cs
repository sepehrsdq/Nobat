using System.Globalization;

namespace Nobat.Domain.Scheduling;

/// <summary>
/// Pure slot arithmetic: given a local working day, a service duration and the already-booked
/// intervals, produce the bookable windows. No database, no clock, no time zone guessing —
/// the caller passes all three explicitly so the result is testable and deterministic.
/// </summary>
public static class SlotPolicy
{
    /// <summary>Start times sit on a 15-minute grid, counted from the beginning of each working window.</summary>
    public const int SlotStepMinutes = 15;

    /// <summary>A patient cannot book something that starts within half an hour.</summary>
    public const int MinimumLeadTimeMinutes = 30;

    /// <summary>The public page never offers slots further than this.</summary>
    public const int MaxAdvanceDays = 60;

    public static IReadOnlyList<Slot> FreeSlots(
        DateOnly localDate,
        IReadOnlyCollection<LocalTimeRange> workingRanges,
        TimeSpan serviceDuration,
        IReadOnlyCollection<UtcInterval> bookedIntervals,
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(workingRanges);
        ArgumentNullException.ThrowIfNull(bookedIntervals);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (serviceDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceDuration),
                serviceDuration,
                "Service duration must be positive.");
        }

        var now = nowUtc.ToUniversalTime();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);

        if (localDate < todayLocal || localDate > todayLocal.AddDays(MaxAdvanceDays))
        {
            return Array.Empty<Slot>();
        }

        var earliestStart = now.AddMinutes(MinimumLeadTimeMinutes);
        var step = TimeSpan.FromMinutes(SlotStepMinutes);
        var windows = MergeWindows(workingRanges);
        var slots = new List<Slot>();

        foreach (var window in windows)
        {
            var windowStart = localDate.ToDateTime(window.Start, DateTimeKind.Unspecified);
            var windowEnd = localDate.ToDateTime(window.End, DateTimeKind.Unspecified);

            for (var cursor = windowStart; cursor + serviceDuration <= windowEnd; cursor = cursor.Add(step))
            {
                var cursorEnd = cursor + serviceDuration;

                if (timeZone.IsInvalidTime(cursor) || timeZone.IsInvalidTime(cursorEnd))
                {
                    // Wall-clock time skipped by a DST jump: no appointment can exist there.
                    continue;
                }

                var candidate = new UtcInterval(ToUtc(cursor, timeZone), ToUtc(cursorEnd, timeZone));

                if (candidate.Start < earliestStart)
                {
                    continue;
                }

                if (IsBooked(candidate, bookedIntervals))
                {
                    continue;
                }

                slots.Add(new Slot(candidate.Start, candidate.End));
            }
        }

        return slots;
    }

    /// <summary>
    /// True when a requested interval collides with an existing booking.
    /// Half-open comparison, so a 09:00–09:30 booking does not block a 09:30 start.
    /// </summary>
    public static bool IsBooked(UtcInterval candidate, IReadOnlyCollection<UtcInterval> bookedIntervals)
    {
        ArgumentNullException.ThrowIfNull(bookedIntervals);

        var candidateNormalized = new UtcInterval(
            candidate.Start.ToUniversalTime(),
            candidate.End.ToUniversalTime());

        foreach (var booked in bookedIntervals)
        {
            var bookedNormalized = new UtcInterval(
                booked.Start.ToUniversalTime(),
                booked.End.ToUniversalTime());

            if (candidateNormalized.Overlaps(bookedNormalized))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<LocalTimeRange> MergeWindows(IReadOnlyCollection<LocalTimeRange> workingRanges)
    {
        ArgumentNullException.ThrowIfNull(workingRanges);

        if (workingRanges.Count == 0)
        {
            return Array.Empty<LocalTimeRange>();
        }

        var ordered = workingRanges.OrderBy(range => range.Start).ToArray();

        foreach (var range in ordered)
        {
            if (range.End <= range.Start)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Working window [{range.Start}, {range.End}) is empty or reversed."),
                    nameof(workingRanges));
            }
        }

        var merged = new List<LocalTimeRange>(ordered.Length);
        var current = ordered[0];

        for (var i = 1; i < ordered.Length; i++)
        {
            var next = ordered[i];

            if (next.Start <= current.End)
            {
                current = current with { End = next.End > current.End ? next.End : current.End };
                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);

        return merged;
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo timeZone)
        => new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone), TimeSpan.Zero);
}
