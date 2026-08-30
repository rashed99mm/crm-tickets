namespace CustomerSupport.Application.Features.Reports.Dtos;

public record SlaPerformanceRow(
    string Priority, int Total, int MetFirstResponse, int BreachedFirstResponse,
    int MetResolution, int BreachedResolution);

public record SlaPerformanceReportDto(IReadOnlyList<SlaPerformanceRow> ByPriority);
