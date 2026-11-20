using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Tenants;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Slug)
            .HasMaxLength(Tenant.SlugMaxLength)
            .IsRequired();

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(Tenant.NameMaxLength)
            .IsRequired();

        builder.Property(tenant => tenant.IsActive)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(tenant => tenant.Slug)
            .IsUnique()
            .HasDatabaseName("UX_tenants_slug");
    }
}
