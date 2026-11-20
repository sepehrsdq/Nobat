using Nobat.Domain.Appointments;
using Nobat.Domain.Common;

namespace Nobat.UnitTests.Appointments;

public sealed class AppointmentTests
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 3, 10, 5, 30, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();

    private static Appointment NewAppointment(DateTimeOffset? start = null, DateTimeOffset? end = null)
        => Appointment.Create(
            TenantId,
            StaffId,
            ServiceId,
            "سارا محمدی",
            "09121234567",
            start ?? Start,
            end ?? Start.AddMinutes(30),
            null,
            Created);

    [Fact]
    public void Create_ValidInput_StartsAsBooked()
    {
        var appointment = NewAppointment();

        appointment.Status.ShouldBe(AppointmentStatus.Booked);
        appointment.IsActive.ShouldBeTrue();
        appointment.PatientPhone.ShouldBe("09121234567");
        appointment.TenantId.ShouldBe(TenantId);
        appointment.CreatedAtUtc.ShouldBe(Created);
    }

    [Fact]
    public void Create_OffsetInput_IsStoredAsUtc()
    {
        var localStart = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(3.5));

        var appointment = NewAppointment(localStart, localStart.AddMinutes(30));

        appointment.StartUtc.Offset.ShouldBe(TimeSpan.Zero);
        appointment.StartUtc.ShouldBe(new DateTimeOffset(2026, 3, 10, 5, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => NewAppointment(Start, Start.AddMinutes(-15)));

        exception.Code.ShouldBe("appointment.range_invalid");
    }

    [Fact]
    public void Create_LongerThanEightHours_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => NewAppointment(Start, Start.AddHours(9)));

        exception.Code.ShouldBe("appointment.duration_invalid");
    }

    [Fact]
    public void Create_InvalidPhone_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => Appointment.Create(
            TenantId,
            StaffId,
            ServiceId,
            "سارا محمدی",
            "12345",
            Start,
            Start.AddMinutes(30),
            null,
            Created));

        exception.Code.ShouldBe("phone.invalid");
    }

    [Fact]
    public void Cancel_BookedAppointment_KeepsTheRowAndStampsTheTime()
    {
        var appointment = NewAppointment();
        var cancelledAt = Created.AddDays(1);

        appointment.Cancel(cancelledAt, "بیمار مرخصی گرفت");

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        appointment.IsActive.ShouldBeFalse();
        appointment.CancelledAtUtc.ShouldBe(cancelledAt);
        appointment.CancellationReason.ShouldBe("بیمار مرخصی گرفت");
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var appointment = NewAppointment();
        appointment.Cancel(Created, null);

        var exception = Should.Throw<DomainRuleViolationException>(() => appointment.Cancel(Created, null));

        exception.Code.ShouldBe("appointment.not_cancellable");
    }

    [Fact]
    public void Reschedule_ToALaterStart_MovesBothEnds()
    {
        var appointment = NewAppointment();
        var newStart = Start.AddDays(2);

        appointment.Reschedule(newStart, newStart.AddMinutes(30), Created);

        appointment.StartUtc.ShouldBe(newStart);
        appointment.EndUtc.ShouldBe(newStart.AddMinutes(30));
        appointment.Status.ShouldBe(AppointmentStatus.Booked);
    }

    [Fact]
    public void Reschedule_WithADifferentDuration_Throws()
    {
        var appointment = NewAppointment();
        var newStart = Start.AddDays(2);

        var exception = Should.Throw<DomainRuleViolationException>(
            () => appointment.Reschedule(newStart, newStart.AddMinutes(45), Created));

        exception.Code.ShouldBe("appointment.duration_invalid");
    }

    [Fact]
    public void Reschedule_IntoThePast_Throws()
    {
        var appointment = NewAppointment();

        var exception = Should.Throw<DomainRuleViolationException>(
            () => appointment.Reschedule(Start, Start.AddMinutes(30), Start.AddMinutes(1)));

        exception.Code.ShouldBe("appointment.start_in_past");
    }

    [Fact]
    public void Reschedule_AfterCancellation_Throws()
    {
        var appointment = NewAppointment();
        appointment.Cancel(Created, null);

        var exception = Should.Throw<DomainRuleViolationException>(
            () => appointment.Reschedule(Start.AddDays(2), Start.AddDays(2).AddMinutes(30), Created));

        exception.Code.ShouldBe("appointment.not_reschedulable");
    }

    [Fact]
    public void Reschedule_WithAnOffset_IsStoredAsUtc()
    {
        var appointment = NewAppointment();
        var localStart = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.FromHours(3.5));

        appointment.Reschedule(localStart, localStart.AddMinutes(30), Created);

        appointment.StartUtc.Offset.ShouldBe(TimeSpan.Zero);
        appointment.StartUtc.ShouldBe(new DateTimeOffset(2026, 3, 12, 5, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Complete_BeforeStart_Throws()
    {
        var appointment = NewAppointment();

        var exception = Should.Throw<DomainRuleViolationException>(
            () => appointment.Complete(Start.AddMinutes(-1)));

        exception.Code.ShouldBe("appointment.not_completable");
    }

    [Fact]
    public void Complete_AfterStart_Succeeds()
    {
        var appointment = NewAppointment();

        appointment.Complete(Start.AddMinutes(5));

        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void MarkNoShow_AfterStart_Succeeds()
    {
        var appointment = NewAppointment();

        appointment.MarkNoShow(Start.AddMinutes(20));

        appointment.Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public void ToInterval_MatchesStartAndEnd()
    {
        var appointment = NewAppointment();

        var interval = appointment.ToInterval();

        interval.Start.ShouldBe(appointment.StartUtc);
        interval.End.ShouldBe(appointment.EndUtc);
    }
}
