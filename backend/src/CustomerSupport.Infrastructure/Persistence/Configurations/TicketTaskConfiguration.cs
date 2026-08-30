using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketTaskConfiguration : IEntityTypeConfiguration<TicketTask>
{
    public void Configure(EntityTypeBuilder<TicketTask> builder)
    {
        builder.ToTable("TicketTasks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TicketId, x.IsDone })
            .HasDatabaseName("IX_TicketTasks_Ticket_IsDone");
    }
}
