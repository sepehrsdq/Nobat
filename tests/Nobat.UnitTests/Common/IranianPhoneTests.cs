using Nobat.Domain.Common;

namespace Nobat.UnitTests.Common;

public sealed class IranianPhoneTests
{
    [Theory]
    [InlineData("09121234567", "09121234567")]
    [InlineData("9121234567", "09121234567")]
    [InlineData("+989121234567", "09121234567")]
    [InlineData("00989121234567", "09121234567")]
    [InlineData("989121234567", "09121234567")]
    [InlineData("0912 123 4567", "09121234567")]
    [InlineData("0912-123-4567", "09121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "09121234567")]
    [InlineData("٠٩١٢١٢٣٤٥٦٧", "09121234567")]
    public void TryNormalize_AcceptedShapes_ReturnsCanonicalNumber(string input, string expected)
    {
        IranianPhone.TryNormalize(input, out var normalized).ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("08121234567")]
    [InlineData("091212345678")]
    [InlineData("0912abc4567")]
    public void TryNormalize_RejectedShapes_ReturnsFalse(string? input)
    {
        IranianPhone.TryNormalize(input, out var normalized).ShouldBeFalse();
        normalized.ShouldBeEmpty();
    }

    [Fact]
    public void Normalize_InvalidNumber_ThrowsWithStableCode()
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => IranianPhone.Normalize("12345"));

        exception.Code.ShouldBe("phone.invalid");
    }

    [Fact]
    public void Mask_CanonicalNumber_HidesTheMiddle()
    {
        IranianPhone.Mask("09121234567").ShouldBe("0912***4567");
    }

    [Fact]
    public void Mask_UnexpectedLength_ReturnsStars()
    {
        IranianPhone.Mask("123").ShouldBe("***");
    }
}
