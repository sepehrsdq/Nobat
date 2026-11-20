namespace Nobat.Application.Public;

public sealed record RequestOtpRequest(string Phone);

/// <summary>
/// <paramref name="DevCode"/> is filled only when <c>Sms:ExposeCodeInResponse</c> is on, which is
/// the local demo configuration. A real deployment leaves it null and the patient checks the SMS.
/// </summary>
public sealed record OtpRequestResult(int ExpiresInSeconds, string? DevCode);

public sealed record BookAppointmentRequest(
    Guid ServiceId,
    Guid StaffId,
    DateTimeOffset StartUtc,
    string PatientName,
    string Phone,
    string Code);

public sealed record BookedAppointmentDto(
    Guid Id,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string StaffName,
    string ServiceName,
    int DurationMinutes,
    long PriceRial);
