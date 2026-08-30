using CustomerSupport.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex("UserId", "Status");
    }
}
