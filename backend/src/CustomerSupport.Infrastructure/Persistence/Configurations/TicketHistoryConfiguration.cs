using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistory");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChangeType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FromValue).HasMaxLength(64);
        builder.Property(x => x.ToValue).HasMaxLength(64);
        builder.Property(x => x.OccurredAt).IsRequired();

        // Newest-first detail read (AC-50).
        builder.HasIndex(x => new { x.TicketId, x.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TicketHistory_Ticket_Occurred");

        // No FK to AspNetUsers on ActorId (US-218, addendum A10): an escalation is a *system*
        // action recorded under the fixed, non-user system actor, so the actor column must be able
        // to hold an identity that is not a user row. The ticket FK above is the meaningful record
        // integrity; actor is an audit attribute like CreatedBy/UpdatedBy elsewhere.
    }
}
