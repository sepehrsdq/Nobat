using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Users;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.TenantId)
            .IsRequired();

        builder.Property(user => user.FullName)
            .HasMaxLength(User.FullNameMaxLength)
            .IsRequired();

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(11)
            .IsFixedLength()
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(256);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(user => new { user.TenantId, user.PhoneNumber })
            .IsUnique()
            .HasDatabaseName("UX_users_tenant_phone");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_users_phone_length", "LEN([PhoneNumber]) = 11");
        });
    }
}
