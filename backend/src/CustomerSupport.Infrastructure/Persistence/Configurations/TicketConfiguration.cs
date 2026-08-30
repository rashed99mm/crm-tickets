using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).IsRequired();

        // String-persisted enums. Reordering the C# type must not renumber existing rows.
        builder.Property(x => x.Priority).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(20);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // FEAT-17 second slice. Explicit default so a generated migration backfills existing rows
        // with "None" rather than an empty string — EF has no other way to know the CLR default.
        builder.Property(x => x.EscalationState).HasMaxLength(16).HasDefaultValue("None");

        // Unfiltered, and deliberately the one exception to the filtered-unique convention: a
        // reference read aloud to a customer must never be reissued, so a soft delete does not
        // free it.
        builder.HasIndex(x => x.Reference)
            .IsUnique()
            .HasDatabaseName("UX_Tickets_Reference");

        // The AC-33 queue filters, which combine.
        builder.HasIndex(x => new { x.Status, x.Priority }).HasDatabaseName("IX_Tickets_Status_Priority");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("IX_Tickets_CustomerId");
        builder.HasIndex(x => x.AssigneeId).HasDatabaseName("IX_Tickets_AssigneeId");

        // No cascades anywhere: the database reinforces what handlers already refuse (AC-15, A10).
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // FEAT-16, AC-117. Nullable — nothing assigns either yet (spec A1).
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // History is reachable only through the aggregate, and only ever appended to. Field access
        // keeps that true for EF's materialisation too — it writes the backing list rather than
        // going through a collection the entity exposes read-only.
        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(Ticket.History))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
