using Nobat.Domain.Common;

namespace Nobat.Domain.Tenants;

/// <summary>
/// A clinic. The tenant root: it has no <c>TenantId</c> of its own and is the only
/// entity outside the global query filter.
/// </summary>
public sealed class Tenant
{
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 40;
    public const int NameMaxLength = 120;

    private Tenant()
    {
    }

    private Tenant(Guid id, string slug, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Slug = slug;
        Name = name;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>Lower-case, URL-safe public identifier: <c>clinic-name.example.ir/{slug}</c>.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Tenant Create(string slug, string name, DateTimeOffset createdAtUtc)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var trimmedName = (name ?? string.Empty).Trim();

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainRuleViolationException(
                "tenant.name_invalid",
                "نام مطب باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        return new Tenant(Guid.CreateVersion7(), normalizedSlug, trimmedName, createdAtUtc.ToUniversalTime());
    }

    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length is < SlugMinLength or > SlugMaxLength)
        {
            return false;
        }

        if (slug[0] == '-' || slug[^1] == '-')
        {
            return false;
        }

        var previousDash = false;

        foreach (var c in slug)
        {
            if (c == '-')
            {
                if (previousDash)
                {
                    return false;
                }

                previousDash = true;
                continue;
            }

            previousDash = false;

            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw new DomainRuleViolationException(
                "tenant.name_invalid",
                "نام مطب باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        Name = trimmed;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string NormalizeSlug(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (!IsValidSlug(normalized))
        {
            throw new DomainRuleViolationException(
                "tenant.slug_invalid",
                "نشانی مطب فقط می‌تواند حروف لاتین کوچک، رقم و خط تیره داشته باشد (۳ تا ۴۰ نویسه).");
        }

        return normalized;
    }
}
