using CustomerSupport.Domain.Entities.ExternalApis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ExternalApiConfigurationConfiguration : IEntityTypeConfiguration<ExternalApiConfiguration>
{
    public void Configure(EntityTypeBuilder<ExternalApiConfiguration> builder)
    {
        builder.ToTable("ExternalApiConfigurations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AuthType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AuthKeyName).HasMaxLength(100);
        builder.Property(x => x.AuthKeyLocation).HasMaxLength(20);
        builder.Property(x => x.AuthValue).HasMaxLength(500);
        builder.Property(x => x.AuthToken).HasMaxLength(2000);
        builder.Property(x => x.AuthTokenUrl).HasMaxLength(500);
        builder.Property(x => x.AuthClientId).HasMaxLength(500);
        builder.Property(x => x.AuthClientSecret).HasMaxLength(500);
        builder.Property(x => x.AuthScope).HasMaxLength(200);
    }
}
