using Nobat.Domain.Tenants;

namespace Nobat.Application.Abstractions;

/// <summary>
/// Looks a clinic up by its public slug. Tenants are the one unfiltered table, so this is
/// the only legal way to resolve a tenant from an untrusted request.
/// </summary>
public interface ITenantResolver
{
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
