using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Domain.Personnel;

/// <summary>
/// A bookable member of the clinic. Optionally linked to a panel <see cref="Users.User"/>;
/// a doctor can exist without ever logging in.
/// </summary>
public sealed class Staff : ITenantEntity
{
    public const int DisplayNameMaxLength = 120;
    public const int TitleMaxLength = 80;

    private Staff()
    {
    }

    private Staff(Guid tenantId, string displayName, string? title, DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        DisplayName = displayName;
        Title = title;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Free text such as «متخصص اطفال».</summary>
    public string? Title { get; private set; }

    public Guid? UserId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Staff Create(Guid tenantId, string displayName, string? title, DateTimeOffset createdAtUtc)
    {
        var trimmedName = (displayName ?? string.Empty).Trim();

        if (trimmedName.Length is 0 or > DisplayNameMaxLength)
        {
            throw new DomainRuleViolationException("staff.name_invalid", "نام پرسنل باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        var trimmedTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        if (trimmedTitle is { Length: > TitleMaxLength })
        {
            throw new DomainRuleViolationException("staff.title_invalid", "عنوان تخصص حداکثر ۸۰ نویسه است.");
        }

        return new Staff(tenantId, trimmedName, trimmedTitle, createdAtUtc.ToUniversalTime());
    }

    public void LinkUser(Guid userId) => UserId = userId;

    public void UnlinkUser() => UserId = null;

    public void Rename(string displayName)
    {
        var trimmed = (displayName ?? string.Empty).Trim();

        if (trimmed.Length is 0 or > DisplayNameMaxLength)
        {
            throw new DomainRuleViolationException("staff.name_invalid", "نام پرسنل باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        DisplayName = trimmed;
    }

    public void ChangeTitle(string? title)
    {
        var trimmed = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

        if (trimmed is { Length: > TitleMaxLength })
        {
            throw new DomainRuleViolationException("staff.title_invalid", "عنوان تخصص حداکثر ۸۰ نویسه است.");
        }

        Title = trimmed;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
