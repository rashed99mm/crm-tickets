using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("CustomerNotes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();

        // Newest first (AC-21).
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CustomerNotes_Customer_Created");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
