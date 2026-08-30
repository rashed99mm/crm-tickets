using CustomerSupport.Domain.Entities.PlatformSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ValueType).HasMaxLength(20).IsRequired();
    }
}
