using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

public class GetSlaPerformanceReportQueryValidator : AbstractValidator<GetSlaPerformanceReportQuery>
{
    public GetSlaPerformanceReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);
    }
}
