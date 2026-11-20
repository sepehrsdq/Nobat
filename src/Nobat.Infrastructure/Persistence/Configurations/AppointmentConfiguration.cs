using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Appointments;
using Nobat.Domain.Catalog;
using Nobat.Domain.Personnel;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.TenantId)
            .IsRequired();

        builder.Property(appointment => appointment.StaffId)
            .IsRequired();

        builder.Property(appointment => appointment.ServiceId)
            .IsRequired();

        builder.Property(appointment => appointment.PatientName)
            .HasMaxLength(Appointment.PatientNameMaxLength)
            .IsRequired();

        builder.Property(appointment => appointment.PatientPhone)
            .HasMaxLength(11)
            .IsFixedLength()
            .IsRequired();

        builder.Property(appointment => appointment.StartUtc)
            .IsRequired();

        builder.Property(appointment => appointment.EndUtc)
            .IsRequired();

        builder.Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(appointment => appointment.Note)
            .HasMaxLength(Appointment.NoteMaxLength);

        builder.Property(appointment => appointment.CreatedAtUtc)
            .IsRequired();

        builder.Property(appointment => appointment.CancellationReason)
            .HasMaxLength(Appointment.CancellationReasonMaxLength);

        // Cross-tenant integrity: both sides of the key carry TenantId.
        builder.HasOne<Staff>()
            .WithMany()
            .HasForeignKey(appointment => new { appointment.TenantId, appointment.StaffId })
            .HasPrincipalKey(staff => new { staff.TenantId, staff.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(appointment => new { appointment.TenantId, appointment.ServiceId })
            .HasPrincipalKey(service => new { service.TenantId, service.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // The red line from doc/_AGENT_BRIEF.md: the database, not the app, refuses a double booking.
        // Cancelled rows are excluded so the slot becomes bookable again without deleting history.
        builder.HasIndex(appointment => new { appointment.TenantId, appointment.StaffId, appointment.StartUtc })
            .IsUnique()
            .HasFilter($"[Status] <> N'{nameof(AppointmentStatus.Cancelled)}'")
            .HasDatabaseName("UX_appointments_tenant_staff_start");

        builder.HasIndex(appointment => new { appointment.TenantId, appointment.Status, appointment.StartUtc })
            .HasDatabaseName("IX_appointments_tenant_status_start");

        builder.HasIndex(appointment => new { appointment.TenantId, appointment.PatientPhone })
            .HasDatabaseName("IX_appointments_tenant_phone");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_appointments_range", "[EndUtc] > [StartUtc]");
        });
    }
}
