using CustomerSupport.Domain.Entities.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class LiveChatMessageConfiguration : IEntityTypeConfiguration<LiveChatMessage>
{
    public void Configure(EntityTypeBuilder<LiveChatMessage> builder)
    {
        builder.ToTable("LiveChatMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SenderType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SenderName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SentAt).IsRequired();

        builder.HasIndex(x => new { x.SessionId, x.SentAt });

        builder.HasOne<LiveChatSession>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
