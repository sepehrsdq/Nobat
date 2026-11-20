using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Abstractions;
using Nobat.Domain.Appointments;
using Nobat.Domain.Catalog;
using Nobat.Domain.Personnel;
using Nobat.Domain.Scheduling;
using Nobat.Domain.Tenants;
using Nobat.Domain.Users;
using Nobat.Domain.Verification;

namespace Nobat.Infrastructure.Persistence;

/// <summary>
/// One database for every tenant. Isolation lives here: every <see cref="ITenantEntity"/>
/// gets a global query filter, and <see cref="Interceptors.TenantIdInterceptor"/> stamps writes.
/// </summary>
public sealed class NobatDbContext : DbContext, IApplicationDbContext
{
    private static readonly MethodInfo SetTenantFilterMethod = typeof(NobatDbContext)
        .GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    public NobatDbContext(DbContextOptions<NobatDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Staff> Staff => Set<Staff>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    /// <summary>
    /// Read by the global filters. <see cref="Guid.Empty"/> never matches a row, so an
    /// unresolved tenant sees nothing instead of everything.
    /// </summary>
    public Guid CurrentTenantId => _tenantContext.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NobatDbContext).Assembly);

        ApplyTenantFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            SetTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
}
