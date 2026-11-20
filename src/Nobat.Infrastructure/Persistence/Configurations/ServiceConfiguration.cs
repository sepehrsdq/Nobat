using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Catalog;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");

        builder.HasKey(service => service.Id);

        builder.HasAlternateKey(service => new { service.TenantId, service.Id })
            .HasName("AK_services_tenant_id");

        builder.Property(service => service.TenantId)
            .IsRequired();

        builder.Property(service => service.Name)
            .HasMaxLength(Service.NameMaxLength)
            .IsRequired();

        builder.Property(service => service.DurationMinutes)
            .IsRequired();

        builder.Property(service => service.PriceRial)
            .IsRequired();

        builder.Property(service => service.IsActive)
            .IsRequired();

        builder.Property(service => service.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(service => new { service.TenantId, service.IsActive })
            .HasDatabaseName("IX_services_tenant_active");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_services_duration",
                "[DurationMinutes] >= 5 AND [DurationMinutes] <= 480 AND [DurationMinutes] % 5 = 0");
            table.HasCheckConstraint("CK_services_price", "[PriceRial] >= 0");
        });
    }
}
