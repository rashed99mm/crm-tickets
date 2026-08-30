using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class SLAPolicyConfiguration : IEntityTypeConfiguration<SLAPolicy>
{
    public void Configure(EntityTypeBuilder<SLAPolicy> builder)
    {
        builder.ToTable("SLAPolicies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Priority).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ResponseTargetHours).HasColumnType("decimal(10,2)");
        builder.Property(x => x.ResolutionTargetHours).HasColumnType("decimal(10,2)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => new { x.Priority, x.IsActive }).HasDatabaseName("IX_SLAPolicies_Priority_Active");
    }
}
