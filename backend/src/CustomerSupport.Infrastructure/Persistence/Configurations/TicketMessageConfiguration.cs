using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.SentAt).IsRequired();

        // Oldest-first timeline read (AC-106).
        builder.HasIndex(x => new { x.TicketId, x.SentAt })
            .HasDatabaseName("IX_TicketMessages_Ticket_SentAt");

        // CC-9/CC-12 idempotency: a retried webhook carrying the same provider message id must be a
        // no-op, not a duplicate insert. Partial because agent-recorded and internal rows carry null.
        builder.HasIndex(x => new { x.Channel, x.ProviderMessageId })
            .IsUnique()
            .HasFilter("[ProviderMessageId] IS NOT NULL");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK from SenderId to AspNetUsers, deliberately: inbound messages are created by
        // anonymous webhooks and recorded under the well-known SystemActors.ChannelIngestion (a
        // non-user), the same reasoning by which ADR-0014 dropped the TicketHistory.ActorId FK.
        // Sender names are still resolved through IIdentityUserService, which returns empty for
        // system actors (GetTicketMessagesQueryHandler).
    }
}
