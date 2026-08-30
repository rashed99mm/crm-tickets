using CustomerSupport.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");

        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TemplateCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(NotificationDelivery.MaxProviderMessageIdLength);
        builder.Property(x => x.ErrorCode).HasMaxLength(50);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);

        builder.HasIndex(x => new { x.Channel, x.ProviderMessageId })
            .HasFilter("[ProviderMessageId] IS NOT NULL")
            .HasDatabaseName("IX_NotificationDeliveries_Channel_ProviderMessageId");
    }
}
