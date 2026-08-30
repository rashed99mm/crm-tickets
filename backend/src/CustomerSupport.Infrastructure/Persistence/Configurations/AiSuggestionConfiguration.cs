using CustomerSupport.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AiSuggestionConfiguration : IEntityTypeConfiguration<AiSuggestion>
{
    public void Configure(EntityTypeBuilder<AiSuggestion> builder)
    {
        builder.ToTable("AiSuggestions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // US-708 — tracking is by ticket: the detail screen's list and any later
        // acceptance-rate query both start from here.
        builder.HasIndex(x => new { x.TicketId, x.CreatedAtUtc })
            .HasDatabaseName("IX_AiSuggestions_Ticket_CreatedAt");

        builder.HasOne<CustomerSupport.Domain.Entities.Tickets.Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
