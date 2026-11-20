using Nobat.Domain.Common;
using Nobat.Domain.Tenants;

namespace Nobat.UnitTests.Tenants;

public sealed class TenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_UpperCaseSlug_IsNormalisedToLowerCase()
    {
        var tenant = Tenant.Create("  Clinic-North  ", "مطب شمال", Now);

        tenant.Slug.ShouldBe("clinic-north");
        tenant.Name.ShouldBe("مطب شمال");
        tenant.IsActive.ShouldBeTrue();
        tenant.CreatedAtUtc.ShouldBe(Now);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-clinic")]
    [InlineData("clinic-")]
    [InlineData("cli--nic")]
    [InlineData("کلینیک")]
    [InlineData("clinic_north")]
    public void Create_InvalidSlug_Throws(string slug)
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => Tenant.Create(slug, "مطب", Now));

        exception.Code.ShouldBe("tenant.slug_invalid");
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => Tenant.Create("clinic", "   ", Now));

        exception.Code.ShouldBe("tenant.name_invalid");
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesTheFlag()
    {
        var tenant = Tenant.Create("clinic", "مطب", Now);

        tenant.Deactivate();
        tenant.IsActive.ShouldBeFalse();

        tenant.Activate();
        tenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_GeneratesVersion7Identifier()
    {
        var tenant = Tenant.Create("clinic", "مطب", Now);

        tenant.Id.ShouldNotBe(Guid.Empty);
        tenant.Id.Version.ShouldBe(7);
    }
}
