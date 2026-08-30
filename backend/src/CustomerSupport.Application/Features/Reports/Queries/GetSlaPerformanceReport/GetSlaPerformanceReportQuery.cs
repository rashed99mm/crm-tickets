using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

/// <summary>Attainment and breach counts by priority — AC-152.</summary>
public record GetSlaPerformanceReportQuery(DateTime From, DateTime To)
    : IQuery<Response<SlaPerformanceReportDto>>;
