using CustomerSupport.Domain.Entities.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class QuickReplyConfiguration : IEntityTypeConfiguration<QuickReply>
{
    public void Configure(EntityTypeBuilder<QuickReply> builder)
    {
        builder.ToTable("QuickReplies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Shortcut).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => x.Shortcut)
            .IsUnique()
            .HasDatabaseName("IX_QuickReplies_Shortcut");
    }
}
