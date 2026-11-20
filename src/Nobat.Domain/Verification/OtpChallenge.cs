using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Domain.Verification;

/// <summary>
/// A one-time code sent to a patient's phone before a booking. Only the hash is stored;
/// the plain code lives for the length of one SMS. Each challenge is single-use and
/// survives a wrong guess only a few times.
/// </summary>
public sealed class OtpChallenge : ITenantEntity
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(10);

    private OtpChallenge()
    {
    }

    private OtpChallenge(
        Guid tenantId,
        string phoneNumber,
        string codeHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        PhoneNumber = phoneNumber;
        CodeHash = codeHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    /// <summary>Canonical <c>09XXXXXXXXX</c>.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public bool IsConsumed => ConsumedAtUtc.HasValue;

    public static OtpChallenge Issue(
        Guid tenantId,
        string phoneNumber,
        string codeHash,
        DateTimeOffset nowUtc,
        TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("A code hash is required.", nameof(codeHash));
        }

        var phone = IranianPhone.Normalize(phoneNumber);
        var window = lifetime ?? DefaultLifetime;

        if (window < TimeSpan.FromSeconds(30) || window > MaxLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), window, "OTP lifetime is out of range.");
        }

        var now = nowUtc.ToUniversalTime();

        return new OtpChallenge(tenantId, phone, codeHash, now, now + window);
    }

    /// <summary>
    /// Consumes the challenge when <paramref name="codeMatches"/> is true and throws otherwise.
    /// A wrong guess still increments <see cref="Attempts"/>, so the caller must persist the
    /// entity even when this method throws.
    /// </summary>
    public void Confirm(bool codeMatches, DateTimeOffset nowUtc)
    {
        var now = nowUtc.ToUniversalTime();

        if (IsConsumed)
        {
            throw new DomainRuleViolationException(
                "otp.already_used",
                "این کد قبلاً استفاده شده است. کد تازه بگیرید.");
        }

        if (now >= ExpiresAtUtc)
        {
            throw new DomainRuleViolationException(
                "otp.expired",
                "کد منقضی شده است. کد تازه بگیرید.");
        }

        if (Attempts >= MaxAttempts)
        {
            throw new DomainRuleViolationException(
                "otp.too_many_attempts",
                "تعداد تلاش‌های اشتباه زیاد است. کد تازه بگیرید.");
        }

        Attempts++;

        if (!codeMatches)
        {
            throw new DomainRuleViolationException(
                "otp.invalid",
                "کد واردشده درست نیست.");
        }

        ConsumedAtUtc = now;
    }
}
