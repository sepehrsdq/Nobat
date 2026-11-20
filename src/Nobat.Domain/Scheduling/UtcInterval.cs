namespace Nobat.Domain.Scheduling;

/// <summary>A half-open interval on the absolute timeline: <c>[Start, End)</c>, always UTC.</summary>
public readonly record struct UtcInterval(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(UtcInterval other) => Start < other.End && other.Start < End;
}
