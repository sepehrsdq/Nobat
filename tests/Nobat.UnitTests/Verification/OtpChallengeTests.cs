using Nobat.Domain.Common;
using Nobat.Domain.Verification;

namespace Nobat.UnitTests.Verification;

public sealed class OtpChallengeTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static OtpChallenge Issue(string phone = "09121234567", string code = "123456")
        => OtpChallenge.Issue(TenantId, phone, "hash:" + code, Now);

    [Fact]
    public void Issue_NormalizesThePhoneNumber()
    {
        var challenge = OtpChallenge.Issue(TenantId, "+98 912 123 4567", "hash", Now);

        challenge.PhoneNumber.ShouldBe("09121234567");
        challenge.TenantId.ShouldBe(TenantId);
        challenge.Attempts.ShouldBe(0);
        challenge.IsConsumed.ShouldBeFalse();
    }

    [Fact]
    public void Issue_RejectsAnInvalidPhone()
    {
        var exception = Should.Throw<DomainRuleViolationException>(
            () => OtpChallenge.Issue(TenantId, "12345", "hash", Now));

        exception.Code.ShouldBe("phone.invalid");
    }

    [Fact]
    public void Issue_UsesTheTwoMinuteDefaultLifetime()
    {
        var challenge = Issue();

        challenge.ExpiresAtUtc.ShouldBe(Now + TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Issue_RejectsAnOutOfRangeLifetime()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => OtpChallenge.Issue(TenantId, "09121234567", "hash", Now, TimeSpan.FromSeconds(5)));

        Should.Throw<ArgumentOutOfRangeException>(
            () => OtpChallenge.Issue(TenantId, "09121234567", "hash", Now, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Confirm_WithTheRightCode_ConsumesTheChallenge()
    {
        var challenge = Issue();

        challenge.Confirm(codeMatches: true, Now.AddSeconds(30));

        challenge.ConsumedAtUtc.ShouldBe(Now.AddSeconds(30));
        challenge.Attempts.ShouldBe(1);
        challenge.IsConsumed.ShouldBeTrue();
    }

    [Fact]
    public void Confirm_WithTheWrongCode_CountsTheAttemptAndThrows()
    {
        var challenge = Issue();

        var exception = Should.Throw<DomainRuleViolationException>(
            () => challenge.Confirm(codeMatches: false, Now.AddSeconds(10)));

        exception.Code.ShouldBe("otp.invalid");
        challenge.Attempts.ShouldBe(1);
        challenge.IsConsumed.ShouldBeFalse();
    }

    [Fact]
    public void Confirm_AfterFiveWrongGuesses_LocksTheChallenge()
    {
        var challenge = Issue();

        for (var attempt = 0; attempt < OtpChallenge.MaxAttempts; attempt++)
        {
            Should.Throw<DomainRuleViolationException>(
                () => challenge.Confirm(codeMatches: false, Now));
        }

        var exception = Should.Throw<DomainRuleViolationException>(
            () => challenge.Confirm(codeMatches: true, Now));

        exception.Code.ShouldBe("otp.too_many_attempts");
        challenge.IsConsumed.ShouldBeFalse();
    }

    [Fact]
    public void Confirm_WhenExpired_Throws()
    {
        var challenge = Issue();

        var exception = Should.Throw<DomainRuleViolationException>(
            () => challenge.Confirm(codeMatches: true, Now + TimeSpan.FromMinutes(3)));

        exception.Code.ShouldBe("otp.expired");
    }

    [Fact]
    public void Confirm_WhenAlreadyConsumed_Throws()
    {
        var challenge = Issue();
        challenge.Confirm(codeMatches: true, Now);

        var exception = Should.Throw<DomainRuleViolationException>(
            () => challenge.Confirm(codeMatches: true, Now.AddSeconds(5)));

        exception.Code.ShouldBe("otp.already_used");
    }
}
