using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;
using Nobat.Domain.Scheduling;

namespace Nobat.Domain.Appointments;

/// <summary>
/// A booked slot. Cancelling changes <see cref="Status"/> and never deletes the row, so the
/// unique slot index can ignore cancelled rows and the slot becomes bookable again.
/// </summary>
public sealed class Appointment : ITenantEntity
{
    public const int PatientNameMaxLength = 120;
    public const int NoteMaxLength = 500;
    public const int CancellationReasonMaxLength = 200;
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    private Appointment()
    {
    }

    private Appointment(
        Guid tenantId,
        Guid staffId,
        Guid serviceId,
        string patientName,
        string patientPhone,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? note,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        StaffId = staffId;
        ServiceId = serviceId;
        PatientName = patientName;
        PatientPhone = patientPhone;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Note = note;
        Status = AppointmentStatus.Booked;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public Guid StaffId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    /// <summary>Canonical <c>09XXXXXXXXX</c>. Never logged unmasked.</summary>
    public string PatientPhone { get; private set; } = string.Empty;

    public DateTimeOffset StartUtc { get; private set; }

    public DateTimeOffset EndUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public bool IsActive => Status == AppointmentStatus.Booked;

    public static Appointment Create(
        Guid tenantId,
        Guid staffId,
        Guid serviceId,
        string patientName,
        string patientPhone,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string? note,
        DateTimeOffset createdAtUtc)
    {
        var trimmedName = (patientName ?? string.Empty).Trim();

        if (trimmedName.Length is 0 or > PatientNameMaxLength)
        {
            throw new DomainRuleViolationException(
                "appointment.patient_name_invalid",
                "نام بیمار باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        var start = startUtc.ToUniversalTime();
        var end = endUtc.ToUniversalTime();

        if (end <= start)
        {
            throw new DomainRuleViolationException(
                "appointment.range_invalid",
                "پایان نوبت باید بعد از شروع آن باشد.");
        }

        if (end - start > MaxDuration)
        {
            throw new DomainRuleViolationException(
                "appointment.duration_invalid",
                "طول نوبت نمی‌تواند بیش از ۸ ساعت باشد.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmedNote is { Length: > NoteMaxLength })
        {
            throw new DomainRuleViolationException(
                "appointment.note_invalid",
                "یادداشت نوبت حداکثر ۵۰۰ نویسه است.");
        }

        return new Appointment(
            tenantId,
            staffId,
            serviceId,
            trimmedName,
            IranianPhone.Normalize(patientPhone),
            start,
            end,
            trimmedNote,
            createdAtUtc.ToUniversalTime());
    }

    public UtcInterval ToInterval() => new(StartUtc, EndUtc);

    public void Cancel(DateTimeOffset nowUtc, string? reason)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainRuleViolationException(
                "appointment.not_cancellable",
                "این نوبت دیگر قابل لغو نیست.");
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmedReason is { Length: > CancellationReasonMaxLength })
        {
            throw new DomainRuleViolationException(
                "appointment.cancellation_reason_invalid",
                "دلیل لغو حداکثر ۲۰۰ نویسه است.");
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = nowUtc.ToUniversalTime();
        CancellationReason = trimmedReason;
    }

    /// <summary>
    /// Moves a booked appointment to a new start. The service, and therefore the length, stays the
    /// same; the free-slot check happens in the use-case, and the unique index is the last word.
    /// </summary>
    public void Reschedule(DateTimeOffset startUtc, DateTimeOffset endUtc, DateTimeOffset nowUtc)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainRuleViolationException(
                "appointment.not_reschedulable",
                "فقط نوبت‌های فعال را می‌توان جابه‌جا کرد.");
        }

        var start = startUtc.ToUniversalTime();
        var end = endUtc.ToUniversalTime();
        var now = nowUtc.ToUniversalTime();

        if (start <= now)
        {
            throw new DomainRuleViolationException(
                "appointment.start_in_past",
                "زمان تازه باید بعد از الان باشد.");
        }

        if (end <= start)
        {
            throw new DomainRuleViolationException(
                "appointment.range_invalid",
                "پایان نوبت باید بعد از شروع آن باشد.");
        }

        if (end - start != EndUtc - StartUtc)
        {
            throw new DomainRuleViolationException(
                "appointment.duration_invalid",
                "طول نوبت در جابه‌جایی نباید تغییر کند.");
        }

        StartUtc = start;
        EndUtc = end;
    }

    public void Complete(DateTimeOffset nowUtc)
    {
        EnsureStarted(nowUtc, "appointment.not_completable", "نوبتی که شروع نشده را نمی‌توان انجام‌شده ثبت کرد.");
        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow(DateTimeOffset nowUtc)
    {
        EnsureStarted(nowUtc, "appointment.not_markable", "نوبتی که شروع نشده را نمی‌توان غیبت ثبت کرد.");
        Status = AppointmentStatus.NoShow;
    }

    private void EnsureStarted(DateTimeOffset nowUtc, string code, string message)
    {
        if (Status != AppointmentStatus.Booked)
        {
            throw new DomainRuleViolationException(
                "appointment.not_booked",
                "فقط نوبت‌های فعال را می‌توان تغییر داد.");
        }

        if (nowUtc.ToUniversalTime() < StartUtc)
        {
            throw new DomainRuleViolationException(code, message);
        }
    }
}
