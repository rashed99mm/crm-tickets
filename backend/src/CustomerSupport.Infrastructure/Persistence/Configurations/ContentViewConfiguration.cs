using CustomerSupport.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentViewConfiguration : IEntityTypeConfiguration<ContentView>
{
    public void Configure(EntityTypeBuilder<ContentView> builder)
    {
        builder.ToTable("ContentViews");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ContentId);

        builder.HasOne<Content>()
            .WithMany()
            .HasForeignKey(x => x.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
