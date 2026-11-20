namespace Nobat.Domain.Scheduling;

/// <summary>
/// The clinic time zone. Every wall-clock value in the domain is interpreted in this zone;
/// storage and API remain UTC.
/// </summary>
public static class ClinicTime
{
    public const string TimeZoneId = "Asia/Tehran";

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
