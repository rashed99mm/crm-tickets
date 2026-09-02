namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// The one place priority comes from (US-923, spec decision 2026-08-31: matrix-only, no override).
/// Pure — a business rule, not a service.
/// </summary>
public static class PriorityMatrix
{
    public static TicketPriority Derive(TicketImpact impact, TicketUrgency urgency) =>
        (impact.Value, urgency.Value) switch
        {
            ("Low", "Low") => TicketPriority.Low,
            ("Low", "Medium") => TicketPriority.Low,
            ("Low", "High") => TicketPriority.Normal,
            ("Medium", "Low") => TicketPriority.Low,
            ("Medium", "Medium") => TicketPriority.Normal,
            ("Medium", "High") => TicketPriority.High,
            ("High", "Low") => TicketPriority.Normal,
            ("High", "Medium") => TicketPriority.High,
            ("High", "High") => TicketPriority.Urgent,
            _ => throw new ArgumentOutOfRangeException(nameof(impact),
                $"No matrix cell for impact '{impact.Value}' x urgency '{urgency.Value}'."),
        };
}
