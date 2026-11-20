namespace Nobat.Application.Appointments;

/// <summary>One row on the secretary's day view. Times are UTC; the client renders Tehran.</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid StaffId,
    string StaffName,
    Guid ServiceId,
    string ServiceName,
    string PatientName,
    string PatientPhone,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DurationMinutes,
    long PriceRial,
    string Status,
    string? Note,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason);

/// <summary>
/// <paramref name="Date"/> echoes the day that was actually read, so a client that sent no date
/// still knows which Tehran day it received.
/// </summary>
public sealed record DayAppointmentsDto(DateOnly Date, IReadOnlyList<AppointmentDto> Items);

public sealed record CancelAppointmentRequest(string? Reason);

public sealed record RescheduleAppointmentRequest(DateTimeOffset StartUtc);

/// <summary>Free starts for moving one appointment, already filtered by its own staff and length.</summary>
public sealed record RescheduleSlotsDto(DateOnly Date, IReadOnlyList<AppointmentSlotDto> Slots);

public sealed record AppointmentSlotDto(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
