using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketLinkConfiguration : IEntityTypeConfiguration<TicketLink>
{
    public void Configure(EntityTypeBuilder<TicketLink> builder)
    {
        builder.ToTable("TicketLinks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LinkType).HasMaxLength(16).IsRequired();

        // One row per (source, target, type) — the database backs the handler's 409 (AC-925.1).
        builder.HasIndex(x => new { x.SourceTicketId, x.TargetTicketId, x.LinkType })
            .IsUnique()
            .HasDatabaseName("UX_TicketLinks_Source_Target_Type");

        builder.HasIndex(x => x.TargetTicketId).HasDatabaseName("IX_TicketLinks_TargetTicketId");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.SourceTicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TargetTicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
