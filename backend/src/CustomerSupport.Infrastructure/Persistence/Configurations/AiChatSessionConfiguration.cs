using CustomerSupport.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("AiChatSessions");

        builder.Property(x => x.ActorId).IsRequired();
        builder.Property(x => x.Scope).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.HasIndex(x => new { x.ActorId, x.Scope, x.Status });
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
