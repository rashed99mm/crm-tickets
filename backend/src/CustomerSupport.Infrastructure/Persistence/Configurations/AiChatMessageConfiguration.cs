using CustomerSupport.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("AiChatMessages");

        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.CitationsJson).HasMaxLength(2000);

        builder.HasIndex(x => new { x.SessionId, x.CreatedAtUtc });
    }
}
