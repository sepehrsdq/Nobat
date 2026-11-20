using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Domain.Users;

/// <summary>A panel account (owner, secretary or doctor). Scoped to one tenant.</summary>
public sealed class User : ITenantEntity
{
    public const int FullNameMaxLength = 120;

    private User()
    {
    }

    private User(Guid tenantId, string fullName, string phoneNumber, UserRole role, DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Role = role;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Canonical form <c>09XXXXXXXXX</c>, also the login identifier.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by the authentication layer. Never a plain password.</summary>
    public string? PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(Guid tenantId, string fullName, string phoneNumber, UserRole role, DateTimeOffset createdAtUtc)
    {
        var trimmedName = (fullName ?? string.Empty).Trim();

        if (trimmedName.Length is 0 or > FullNameMaxLength)
        {
            throw new DomainRuleViolationException("user.name_invalid", "نام کاربر باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        var normalizedPhone = IranianPhone.Normalize(phoneNumber);

        return new User(tenantId, trimmedName, normalizedPhone, role, createdAtUtc.ToUniversalTime());
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainRuleViolationException("user.password_missing", "هش رمز نمی‌تواند خالی باشد.");
        }

        PasswordHash = passwordHash;
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
