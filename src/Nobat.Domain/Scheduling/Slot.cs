namespace Nobat.Domain.Scheduling;

/// <summary>A bookable window, already resolved to UTC. Display converts to the clinic time zone.</summary>
public readonly record struct Slot(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public TimeSpan Duration => EndUtc - StartUtc;
}
