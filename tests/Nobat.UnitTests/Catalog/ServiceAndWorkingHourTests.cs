using Nobat.Domain.Catalog;
using Nobat.Domain.Common;
using Nobat.Domain.Personnel;
using Nobat.Domain.Scheduling;

namespace Nobat.UnitTests.Catalog;

public sealed class ServiceAndWorkingHourTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StaffId = Guid.NewGuid();

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(480)]
    public void Service_Create_AcceptsValidDurations(int minutes)
    {
        var service = Service.Create(TenantId, "ویزیت", minutes, 500_000, Now);

        service.DurationMinutes.ShouldBe(minutes);
        service.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(485)]
    public void Service_Create_RejectsInvalidDurations(int minutes)
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => Service.Create(TenantId, "ویزیت", minutes, 0, Now));

        exception.Code.ShouldBe("service.duration_invalid");
    }

    [Fact]
    public void Service_NegativePrice_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => Service.Create(TenantId, "ویزیت", 30, -1, Now));

        exception.Code.ShouldBe("service.price_invalid");
    }

    [Fact]
    public void WorkingHour_ValidWindow_IsCreated()
    {
        var workingHour = WorkingHour.Create(
            TenantId,
            StaffId,
            DayOfWeek.Saturday,
            new TimeOnly(9, 0),
            new TimeOnly(13, 0));

        workingHour.ToLocalRange().ShouldBe(new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(13, 0)));
    }

    [Fact]
    public void WorkingHour_ReversedWindow_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => WorkingHour.Create(
            TenantId,
            StaffId,
            DayOfWeek.Saturday,
            new TimeOnly(13, 0),
            new TimeOnly(9, 0)));

        exception.Code.ShouldBe("working_hour.range_invalid");
    }

    [Fact]
    public void WorkingHour_WindowShorterThanFifteenMinutes_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => WorkingHour.Create(
            TenantId,
            StaffId,
            DayOfWeek.Saturday,
            new TimeOnly(9, 0),
            new TimeOnly(9, 10)));

        exception.Code.ShouldBe("working_hour.too_short");
    }

    [Fact]
    public void Staff_Create_TrimsNameAndKeepsOptionalTitle()
    {
        var staff = Staff.Create(TenantId, "  دکتر رضایی ", "  ", Now);

        staff.DisplayName.ShouldBe("دکتر رضایی");
        staff.Title.ShouldBeNull();
        staff.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Staff_EmptyName_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => Staff.Create(TenantId, "   ", null, Now));

        exception.Code.ShouldBe("staff.name_invalid");
    }
}
