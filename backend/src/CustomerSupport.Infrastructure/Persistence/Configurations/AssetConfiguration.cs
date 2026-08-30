using CustomerSupport.Domain.Entities.Assets;
using CustomerSupport.Domain.Entities.Customers;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();

        builder.HasIndex(x => x.StoredFileName)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Assets_StoredFileName");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerAttachmentConfiguration : IEntityTypeConfiguration<CustomerAttachment>
{
    public void Configure(EntityTypeBuilder<CustomerAttachment> builder)
    {
        builder.ToTable("CustomerAttachments");
        builder.HasKey(x => x.Id);

        // One live link per asset; unlinking frees the asset rather than orphaning the constraint.
        builder.HasIndex(x => x.AssetId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_CustomerAttachments_Asset");

        builder.HasIndex(x => x.CustomerId).HasDatabaseName("IX_CustomerAttachments_Customer");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");
        builder.HasKey(x => x.Id);

        // One live link per asset; unlinking frees the asset rather than orphaning the constraint.
        builder.HasIndex(x => x.AssetId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TicketAttachments_Asset");

        builder.HasIndex(x => x.TicketId).HasDatabaseName("IX_TicketAttachments_Ticket");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
