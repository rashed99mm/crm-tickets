using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketNoteConfiguration : IEntityTypeConfiguration<TicketNote>
{
    public void Configure(EntityTypeBuilder<TicketNote> builder)
    {
        builder.ToTable("TicketNotes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TicketId, x.IsInternal })
            .HasDatabaseName("IX_TicketNotes_Ticket_Internal");
    }
}
