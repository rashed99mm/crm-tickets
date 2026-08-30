using CustomerSupport.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentVoteConfiguration : IEntityTypeConfiguration<ContentVote>
{
    public void Configure(EntityTypeBuilder<ContentVote> builder)
    {
        builder.ToTable("ContentVotes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ContentId, x.UserId }).IsUnique();

        builder.HasOne<Content>()
            .WithMany()
            .HasForeignKey(x => x.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
