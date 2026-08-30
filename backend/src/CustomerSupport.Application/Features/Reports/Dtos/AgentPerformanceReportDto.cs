namespace CustomerSupport.Application.Features.Reports.Dtos;

public record AgentPerformanceRow(Guid AgentId, string AgentName, int TicketsResolved, double AvgHandleMinutes);

public record AgentPerformanceReportDto(IReadOnlyList<AgentPerformanceRow> ByAgent);
