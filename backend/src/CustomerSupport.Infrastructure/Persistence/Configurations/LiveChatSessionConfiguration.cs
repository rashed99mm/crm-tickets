using CustomerSupport.Domain.Entities.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class LiveChatSessionConfiguration : IEntityTypeConfiguration<LiveChatSession>
{
    public void Configure(EntityTypeBuilder<LiveChatSession> builder)
    {
        builder.ToTable("LiveChatSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(120);
        builder.Property(x => x.CustomerEmail).HasMaxLength(320);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.SessionTokenHash).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
