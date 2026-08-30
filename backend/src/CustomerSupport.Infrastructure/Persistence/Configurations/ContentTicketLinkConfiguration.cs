using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class ContentTicketLinkConfiguration : IEntityTypeConfiguration<ContentTicketLink>
{
    public void Configure(EntityTypeBuilder<ContentTicketLink> builder)
    {
        builder.ToTable("ContentTicketLinks");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TicketId, x.ContentId }).IsUnique();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Content>()
            .WithMany()
            .HasForeignKey(x => x.ContentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
