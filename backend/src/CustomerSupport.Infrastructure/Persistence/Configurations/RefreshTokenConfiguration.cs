using CustomerSupport.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.RevokedBy).HasMaxLength(100);
        builder.Property(x => x.ReplacedByToken).HasMaxLength(500);

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
