using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

/// <summary>AC-154.</summary>
public class GetTicketVolumeReportQueryValidator : AbstractValidator<GetTicketVolumeReportQuery>
{
    private static readonly string[] AllowedGroupings = ["day", "week", "month"];

    public GetTicketVolumeReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);

        RuleFor(x => x.GroupBy)
            .Must(g => AllowedGroupings.Contains(g))
            .WithErrorCode(ApplicationErrors.Validation.REPORT_GROUP_BY_INVALID);
    }
}
