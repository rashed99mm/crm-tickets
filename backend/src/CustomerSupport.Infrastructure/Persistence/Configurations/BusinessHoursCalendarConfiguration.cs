using CustomerSupport.Domain.Entities.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class BusinessHoursCalendarConfiguration : IEntityTypeConfiguration<BusinessHoursCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessHoursCalendar> builder)
    {
        builder.ToTable("BusinessHoursCalendars");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OpenTime).HasColumnType("time");
        builder.Property(x => x.CloseTime).HasColumnType("time");
        builder.HasIndex(x => new { x.BranchId, x.DayOfWeek })
            .HasDatabaseName("IX_BusinessHoursCalendars_Branch_Day");
    }
}
