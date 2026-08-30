using CustomerSupport.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentCategoryConfiguration : IEntityTypeConfiguration<ContentCategory>
{
    public void Configure(EntityTypeBuilder<ContentCategory> builder)
    {
        builder.ToTable("ContentCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.Name, x.ParentId }).IsUnique();

        builder.HasOne<ContentCategory>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
