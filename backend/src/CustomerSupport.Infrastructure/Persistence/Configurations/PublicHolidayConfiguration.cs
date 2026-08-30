using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.ToTable("PublicHolidays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HolidayDate).HasColumnType("date");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.HolidayDate })
            .HasDatabaseName("IX_PublicHolidays_Branch_Date");
    }
}
