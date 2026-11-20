using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Personnel;
using Nobat.Domain.Scheduling;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class WorkingHourConfiguration : IEntityTypeConfiguration<WorkingHour>
{
    public void Configure(EntityTypeBuilder<WorkingHour> builder)
    {
        builder.ToTable("working_hours");

        builder.HasKey(workingHour => workingHour.Id);

        builder.Property(workingHour => workingHour.TenantId)
            .IsRequired();

        builder.Property(workingHour => workingHour.StaffId)
            .IsRequired();

        builder.Property(workingHour => workingHour.DayOfWeek)
            .IsRequired();

        builder.Property(workingHour => workingHour.StartTime)
            .IsRequired();

        builder.Property(workingHour => workingHour.EndTime)
            .IsRequired();

        builder.Property(workingHour => workingHour.IsActive)
            .IsRequired();

        builder.HasOne<Staff>()
            .WithMany()
            .HasForeignKey(workingHour => new { workingHour.TenantId, workingHour.StaffId })
            .HasPrincipalKey(staff => new { staff.TenantId, staff.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workingHour => new { workingHour.TenantId, workingHour.StaffId, workingHour.DayOfWeek })
            .HasDatabaseName("IX_working_hours_tenant_staff_day");

        builder.HasIndex(workingHour => new { workingHour.TenantId, workingHour.StaffId, workingHour.DayOfWeek, workingHour.StartTime })
            .IsUnique()
            .HasDatabaseName("UX_working_hours_tenant_staff_day_start");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_working_hours_range", "[EndTime] > [StartTime]");
        });
    }
}
