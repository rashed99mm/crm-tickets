using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Reports.Dtos;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

/// <summary>Throughput and approximate handle time per agent — AC-153.</summary>
public record GetAgentPerformanceReportQuery(DateTime From, DateTime To)
    : IQuery<Response<AgentPerformanceReportDto>>;
