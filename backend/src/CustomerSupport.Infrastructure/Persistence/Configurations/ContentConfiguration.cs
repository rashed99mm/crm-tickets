using CustomerSupport.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.ToTable("Contents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FeaturedImageUrl).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Tags).HasJsonConversion();
        // FEAT-11, AC-169. Every article's true version is 1 from the moment it's created — the
        // CLR default (0) would be wrong for every row, existing or new, without this.
        builder.Property(x => x.Version).HasDefaultValue(1);
        builder.HasIndex(x => x.AuthorId);
        builder.HasIndex(x => x.ContentType);
        builder.HasIndex(x => x.Status);
    }
}
