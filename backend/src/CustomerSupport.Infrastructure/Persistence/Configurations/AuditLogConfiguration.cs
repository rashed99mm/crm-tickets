using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using CustomerSupport.Domain.Entities.Audit;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.UserName)
            .HasMaxLength(256);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.OldValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.NewValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(50);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.EntityType);
        builder.HasIndex(x => x.EntityId);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.CreatedAt);
    }
}
