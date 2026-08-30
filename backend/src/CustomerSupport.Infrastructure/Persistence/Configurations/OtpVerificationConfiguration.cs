using CustomerSupport.Domain.Entities.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class OtpVerificationConfiguration : IEntityTypeConfiguration<OtpVerification>
{
    public void Configure(EntityTypeBuilder<OtpVerification> builder)
    {
        builder.ToTable("OtpVerifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Contact).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.LastSentAtUtc);
        builder.Property(x => x.FailedAttemptCount).IsRequired();
        builder.Property(x => x.IsVerified).IsRequired();
        builder.Property(x => x.IsInvalidated).IsRequired();

        // Optimistic concurrency: the verify handler's save fails if another request changed the row.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.UserId, x.Type, x.IsVerified });
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
