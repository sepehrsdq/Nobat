using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Application.Public;

/// <summary>
/// Free slots for one staff member, one service and one local (Tehran) day.
/// The heavy lifting is <see cref="Nobat.Domain.Scheduling.SlotPolicy"/>; this handler only
/// validates the public identifiers and delegates the arithmetic.
/// </summary>
public sealed class GetPublicSlotsHandler
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextSetter _tenantContext;
    private readonly TimeProvider _clock;

    public GetPublicSlotsHandler(
        IApplicationDbContext db,
        ITenantResolver tenantResolver,
        ITenantContextSetter tenantContext,
        TimeProvider clock)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PublicSlotDto>> HandleAsync(
        string slug,
        Guid serviceId,
        Guid staffId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantResolver.FindBySlugAsync(slug, cancellationToken)
            ?? throw new DomainRuleViolationException("tenant.not_found", "چنین مطبی وجود ندارد.");

        _tenantContext.SetTenant(tenant.Id);

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == serviceId && candidate.IsActive,
                cancellationToken)
            ?? throw new DomainRuleViolationException("service.not_found", "این خدمت در دسترس نیست.");

        var staffExists = await _db.Staff
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.Id == staffId && candidate.IsActive,
                cancellationToken);

        if (!staffExists)
        {
            throw new DomainRuleViolationException("staff.not_found", "این پرسنل در دسترس نیست.");
        }

        var slots = await SlotLookup.FreeSlotsAsync(
            _db,
            staffId,
            service.DurationMinutes,
            date,
            _clock.GetUtcNow(),
            cancellationToken);

        return slots
            .Select(slot => new PublicSlotDto(slot.StartUtc, slot.EndUtc))
            .ToArray();
    }
}
