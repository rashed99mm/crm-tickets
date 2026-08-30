using CustomerSupport.Domain.Entities.Survey;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

public class SurveyResponseConfiguration : IEntityTypeConfiguration<SurveyResponse>
{
    public void Configure(EntityTypeBuilder<SurveyResponse> builder)
    {
        builder.ToTable("SurveyResponses");

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.FreeText).HasMaxLength(SurveyResponse.MaxFreeTextLength);

        builder.HasIndex(x => x.TicketId).IsUnique();
    }
}