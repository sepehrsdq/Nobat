namespace Nobat.Application.Abstractions;

/// <summary>
/// The tenant resolved for the current request. <see cref="TenantId"/> is null only for
/// unauthenticated or cross-tenant admin work, which is the single documented exception.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }
}

/// <summary>Write side of the tenant context, used by the request pipeline only.</summary>
public interface ITenantContextSetter
{
    void SetTenant(Guid tenantId);
}
