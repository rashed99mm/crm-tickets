using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(32);

        // FEAT-16, AC-117. Nullable — nothing assigns this yet (spec A1).
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-deleting a customer frees their email for reuse instead of burning it on
        // a row the user can no longer see (AC-9, AC-16, ADR-0006).
        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Customers_Email");

        builder.HasIndex(x => x.Name).HasDatabaseName("IX_Customers_Name");
    }
}
