using Nobat.Domain.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Domain.Catalog;

/// <summary>A bookable service with a fixed duration and price.</summary>
public sealed class Service : ITenantEntity
{
    public const int NameMaxLength = 120;
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 480;
    public const int DurationStepMinutes = 5;

    private Service()
    {
    }

    private Service(Guid tenantId, string name, int durationMinutes, long priceRial, DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Name = name;
        DurationMinutes = durationMinutes;
        PriceRial = priceRial;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    /// <summary>Price in Rial. Rial is an integer currency; no decimals are stored.</summary>
    public long PriceRial { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Service Create(Guid tenantId, string name, int durationMinutes, long priceRial, DateTimeOffset createdAtUtc)
    {
        var trimmedName = (name ?? string.Empty).Trim();

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainRuleViolationException("service.name_invalid", "نام خدمت باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        EnsureDurationIsValid(durationMinutes);

        if (priceRial < 0)
        {
            throw new DomainRuleViolationException("service.price_invalid", "قیمت خدمت نمی‌تواند منفی باشد.");
        }

        return new Service(tenantId, trimmedName, durationMinutes, priceRial, createdAtUtc.ToUniversalTime());
    }

    public void ChangeDuration(int durationMinutes)
    {
        EnsureDurationIsValid(durationMinutes);
        DurationMinutes = durationMinutes;
    }

    public void ChangePrice(long priceRial)
    {
        if (priceRial < 0)
        {
            throw new DomainRuleViolationException("service.price_invalid", "قیمت خدمت نمی‌تواند منفی باشد.");
        }

        PriceRial = priceRial;
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw new DomainRuleViolationException("service.name_invalid", "نام خدمت باید بین ۱ تا ۱۲۰ نویسه باشد.");
        }

        Name = trimmed;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void EnsureDurationIsValid(int durationMinutes)
    {
        if (durationMinutes is < MinDurationMinutes or > MaxDurationMinutes || durationMinutes % DurationStepMinutes != 0)
        {
            throw new DomainRuleViolationException(
                "service.duration_invalid",
                "مدت خدمت باید مضربی از ۵ و بین ۵ تا ۴۸۰ دقیقه باشد.");
        }
    }
}
