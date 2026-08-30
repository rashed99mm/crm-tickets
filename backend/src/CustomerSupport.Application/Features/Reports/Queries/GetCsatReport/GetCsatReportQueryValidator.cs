using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetCsatReport;

public class GetCsatReportQueryValidator : AbstractValidator<GetCsatReportQuery>
{
    public GetCsatReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);
    }
}
