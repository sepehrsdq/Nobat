namespace Nobat.Domain.Abstractions;

/// <summary>
/// Marker for entities whose rows belong to a single tenant.
/// Every implementation gets a global query filter and a mandatory <c>TenantId</c> column.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
