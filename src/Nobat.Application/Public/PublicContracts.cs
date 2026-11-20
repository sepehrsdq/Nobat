namespace Nobat.Application.Public;

public sealed record PublicClinicDto(
    string Slug,
    string Name,
    IReadOnlyList<PublicServiceDto> Services,
    IReadOnlyList<PublicStaffDto> Staff);

public sealed record PublicServiceDto(Guid Id, string Name, int DurationMinutes, long PriceRial);

public sealed record PublicStaffDto(Guid Id, string DisplayName, string? Title);

public sealed record PublicSlotDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
