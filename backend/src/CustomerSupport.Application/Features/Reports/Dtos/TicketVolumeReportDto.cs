namespace CustomerSupport.Application.Features.Reports.Dtos;

/// <summary>One named bucket's count — shared shape across every report's breakdowns.</summary>
public record ReportBucket(string Key, int Count);

/// <summary>Ticket volume, three independent breakdowns over one date range (AC-149..AC-151,
/// spec A8 — not a single period×category×priority cross-tab).</summary>
public record TicketVolumeReportDto(
    IReadOnlyList<ReportBucket> ByPeriod,
    IReadOnlyList<ReportBucket> ByCategory,
    IReadOnlyList<ReportBucket> ByPriority);
