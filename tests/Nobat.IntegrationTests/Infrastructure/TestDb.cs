using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Catalog;
using Nobat.Domain.Personnel;
using Nobat.Domain.Tenants;
using Nobat.Infrastructure.Persistence;
using Nobat.Infrastructure.Persistence.Interceptors;
using Nobat.Infrastructure.Tenancy;

namespace Nobat.IntegrationTests.Infrastructure;

internal sealed class TestTenantContext : ITenantContext, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}

internal static class TestDb
{
    public static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    public static NobatDbContext Create(string connectionString, Guid? tenantId)
    {
        var tenantContext = new TestTenantContext();

        if (tenantId.HasValue)
        {
            tenantContext.SetTenant(tenantId.Value);
        }

        var options = new DbContextOptionsBuilder<NobatDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new TenantIdInterceptor(tenantContext))
            .Options;

        return new NobatDbContext(options, tenantContext);
    }

    public static async Task<Clinic> SeedClinicAsync(string connectionString, string slug)
    {
        var tenant = Tenant.Create(slug, "مطب " + slug, Now);

        await using (var db = Create(connectionString, tenantId: null))
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var staff = Staff.Create(tenant.Id, "دکتر " + slug, "عمومی", Now);
        var service = Service.Create(tenant.Id, "ویزیت", 30, 500_000, Now);

        await using (var db = Create(connectionString, tenant.Id))
        {
            db.Staff.Add(staff);
            db.Services.Add(service);
            await db.SaveChangesAsync();
        }

        return new Clinic(tenant, staff, service);
    }
}

internal sealed record Clinic(Tenant Tenant, Staff Staff, Service Service);
