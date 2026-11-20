using Nobat.Application.Abstractions;

namespace Nobat.Infrastructure.Tenancy;

/// <summary>
/// Scoped holder for the tenant resolved from the request. Registered once per request;
/// the middleware sets it, the DbContext reads it.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}

/// <summary>
/// Tenant-less context for design-time tooling and cross-tenant admin work.
/// Writes through this context are rejected by the interceptor on purpose.
/// </summary>
public sealed class UnresolvedTenantContext : ITenantContext
{
    public Guid? TenantId => null;

    public bool HasTenant => false;
}
