using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Personnel;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("staff");

        builder.HasKey(staff => staff.Id);

        // Composite key so child tables can reference (TenantId, Id) and a cross-tenant
        // foreign key becomes impossible at the database level.
        builder.HasAlternateKey(staff => new { staff.TenantId, staff.Id })
            .HasName("AK_staff_tenant_id");

        builder.Property(staff => staff.TenantId)
            .IsRequired();

        builder.Property(staff => staff.DisplayName)
            .HasMaxLength(Staff.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(staff => staff.Title)
            .HasMaxLength(Staff.TitleMaxLength);

        builder.Property(staff => staff.IsActive)
            .IsRequired();

        builder.Property(staff => staff.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(staff => new { staff.TenantId, staff.IsActive })
            .HasDatabaseName("IX_staff_tenant_active");
    }
}
