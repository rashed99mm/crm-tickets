using CustomerSupport.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("ContentVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeSummary).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.TitleSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BodySnapshot).IsRequired();
        builder.HasIndex(x => new { x.ContentId, x.VersionNumber });

        builder.HasOne<Content>()
            .WithMany()
            .HasForeignKey(x => x.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
