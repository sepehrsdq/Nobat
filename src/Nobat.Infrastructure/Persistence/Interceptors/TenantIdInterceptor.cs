using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nobat.Application.Abstractions;
using Nobat.Domain.Abstractions;

namespace Nobat.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The only place that assigns <c>TenantId</c>. It runs before every insert/update/delete:
/// missing tenant means the write is refused, and a mismatched tenant means the write is
/// refused as well. No handler is allowed to set it by hand.
/// </summary>
public sealed class TenantIdInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantIdInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampTenant(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var pending = context.ChangeTracker
            .Entries<ITenantEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        var tenantId = _tenantContext.TenantId;

        if (tenantId is null)
        {
            throw new TenantIsolationException(
                "Refusing to write tenant data while no tenant is resolved. Resolve the tenant before saving.");
        }

        foreach (var entry in pending)
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId.Value;
                continue;
            }

            if (entry.Entity.TenantId != tenantId.Value)
            {
                throw new TenantIsolationException(
                    $"Refusing a cross-tenant write on {entry.Metadata.ClrType.Name}: " +
                    "the entity belongs to a different tenant than the current request.");
            }
        }
    }
}
