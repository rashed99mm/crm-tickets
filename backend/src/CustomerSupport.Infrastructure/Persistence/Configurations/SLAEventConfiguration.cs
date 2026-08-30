using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class SLAEventConfiguration : IEntityTypeConfiguration<SLAEvent>
{
    public void Configure(EntityTypeBuilder<SLAEvent> builder)
    {
        builder.ToTable("SLAEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetType).HasMaxLength(16).IsRequired();

        // The breach scanner's "already recorded?" check (AC-132).
        builder.HasIndex(x => new { x.TicketId, x.TargetType }).HasDatabaseName("IX_SLAEvents_Ticket_TargetType");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
