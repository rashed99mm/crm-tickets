using CustomerSupport.Application.Errors;
using FluentValidation;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

public class GetAgentPerformanceReportQueryValidator : AbstractValidator<GetAgentPerformanceReportQuery>
{
    public GetAgentPerformanceReportQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCode(ApplicationErrors.Validation.REPORT_RANGE_INVALID);
    }
}
