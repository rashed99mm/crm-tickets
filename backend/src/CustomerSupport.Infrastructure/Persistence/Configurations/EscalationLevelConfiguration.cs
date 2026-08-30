using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class EscalationLevelConfiguration : IEntityTypeConfiguration<EscalationLevel>
{
    public void Configure(EntityTypeBuilder<EscalationLevel> builder)
    {
        builder.ToTable("EscalationLevels");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Level).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BreachMinutes).IsRequired();
        builder.Property(x => x.TargetRole).HasMaxLength(64);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Level).HasDatabaseName("IX_EscalationLevels_Level").IsUnique();
    }
}
