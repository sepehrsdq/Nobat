using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Application.Public;

/// <summary>What the public booking page needs before asking for a phone number.</summary>
public sealed class GetPublicClinicHandler
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextSetter _tenantContext;

    public GetPublicClinicHandler(
        IApplicationDbContext db,
        ITenantResolver tenantResolver,
        ITenantContextSetter tenantContext)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
    }

    public async Task<PublicClinicDto> HandleAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantResolver.FindBySlugAsync(slug, cancellationToken)
            ?? throw NotFound();

        _tenantContext.SetTenant(tenant.Id);

        var services = await _db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => new PublicServiceDto(
                service.Id,
                service.Name,
                service.DurationMinutes,
                service.PriceRial))
            .ToListAsync(cancellationToken);

        var staff = await _db.Staff
            .AsNoTracking()
            .Where(member => member.IsActive)
            .OrderBy(member => member.DisplayName)
            .Select(member => new PublicStaffDto(member.Id, member.DisplayName, member.Title))
            .ToListAsync(cancellationToken);

        return new PublicClinicDto(tenant.Slug, tenant.Name, services, staff);
    }

    private static DomainRuleViolationException NotFound() => new(
        "tenant.not_found",
        "چنین مطبی وجود ندارد.");
}
