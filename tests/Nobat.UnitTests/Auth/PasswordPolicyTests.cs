using Nobat.Application.Auth;
using Nobat.Domain.Common;

namespace Nobat.UnitTests.Auth;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Nobat1405")]
    [InlineData("a1234567")]
    [InlineData("Secret-99")]
    public void EnsureValid_AcceptedPasswords_DoNotThrow(string password)
    {
        Should.NotThrow(() => PasswordPolicy.EnsureValid(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    public void EnsureValid_RejectedPasswords_ThrowWithStableCode(string? password)
    {
        var exception = Should.Throw<DomainRuleViolationException>(() => PasswordPolicy.EnsureValid(password));

        exception.Code.ShouldBe("auth.password_weak");
    }

    [Fact]
    public void EnsureValid_TooLong_Throws()
    {
        var password = new string('a', PasswordPolicy.MaxLength) + "1";

        var exception = Should.Throw<DomainRuleViolationException>(() => PasswordPolicy.EnsureValid(password));

        exception.Code.ShouldBe("auth.password_weak");
    }
}
