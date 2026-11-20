using Nobat.Domain.Common;

namespace Nobat.Application.Auth;

/// <summary>Panel password rules. Short and explicit; no dictionary check in v1.0.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static void EnsureValid(string? password)
    {
        var value = password ?? string.Empty;

        if (value.Length is < MinLength or > MaxLength)
        {
            throw new DomainRuleViolationException(
                "auth.password_weak",
                "رمز عبور باید بین ۸ تا ۱۲۸ نویسه باشد.");
        }

        if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit))
        {
            throw new DomainRuleViolationException(
                "auth.password_weak",
                "رمز عبور باید هم حرف و هم رقم داشته باشد.");
        }
    }
}
