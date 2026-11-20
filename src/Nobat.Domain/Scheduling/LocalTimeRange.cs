namespace Nobat.Domain.Scheduling;

/// <summary>A half-open time range on the clinic's wall clock: <c>[Start, End)</c>.</summary>
public readonly record struct LocalTimeRange(TimeOnly Start, TimeOnly End)
{
    public TimeSpan Duration => End - Start;
}
