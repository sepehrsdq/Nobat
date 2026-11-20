using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nobat.Domain.Verification;

namespace Nobat.Infrastructure.Persistence.Configurations;

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.TenantId)
            .IsRequired();

        builder.Property(challenge => challenge.PhoneNumber)
            .HasMaxLength(11)
            .IsFixedLength()
            .IsRequired();

        builder.Property(challenge => challenge.CodeHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(challenge => challenge.CreatedAtUtc)
            .IsRequired();

        builder.Property(challenge => challenge.ExpiresAtUtc)
            .IsRequired();

        builder.Property(challenge => challenge.Attempts)
            .IsRequired();

        // The booking path looks up the newest challenge for one phone inside one clinic.
        builder.HasIndex(challenge => new { challenge.TenantId, challenge.PhoneNumber, challenge.CreatedAtUtc })
            .HasDatabaseName("IX_otp_challenges_tenant_phone_created");

        // A cleanup job can delete everything older than the retention window with one scan.
        builder.HasIndex(challenge => challenge.ExpiresAtUtc)
            .HasDatabaseName("IX_otp_challenges_expires");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_otp_challenges_phone_length", "LEN([PhoneNumber]) = 11");
        });
    }
}
