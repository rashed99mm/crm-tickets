using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("TicketTags");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value).HasMaxLength(30).IsRequired();

        // One tag once per ticket (AC-924.1) — the database backs what the handler refuses.
        builder.HasIndex(x => new { x.TicketId, x.Value })
            .IsUnique()
            .HasDatabaseName("UX_TicketTags_TicketId_Value");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
