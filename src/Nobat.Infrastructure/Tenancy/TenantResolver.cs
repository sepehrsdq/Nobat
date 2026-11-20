using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Tenants;

namespace Nobat.Infrastructure.Tenancy;

/// <summary>Resolves a clinic from its public slug. Tenants are not tenant-filtered.</summary>
public sealed class TenantResolver : ITenantResolver
{
    private readonly IApplicationDbContext _db;

    public TenantResolver(IApplicationDbContext db)
    {
        _db = db;
    }

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            return Task.FromResult<Tenant?>(null);
        }

        return _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(tenant => tenant.Slug == normalized && tenant.IsActive, cancellationToken);
    }
}
