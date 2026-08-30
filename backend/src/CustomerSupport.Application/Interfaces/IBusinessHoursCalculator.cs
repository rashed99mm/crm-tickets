namespace CustomerSupport.Application.Interfaces;

/// <summary>US-215, AC-225..AC-227 — advances a UTC instant by a number of working hours against a
/// branch's business-hours calendar.</summary>
public interface IBusinessHoursCalculator
{
    /// <summary>Advances <paramref name="start"/> by <paramref name="hours"/> of working time for the
    /// branch's configured calendar, returning a UTC instant. Falls back to plain wall-clock addition
    /// when <paramref name="branchId"/> is null or the branch has no configured calendar (US-215,
    /// AC-227) — the exact behavior every existing SLA test already assumes, unchanged.</summary>
    /// <param name="start">The UTC start instant.</param>
    /// <param name="hours">The number of working hours to add.</param>
    /// <param name="branchId">The ticket's branch, or null when none is set.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DateTime> AddBusinessHours(DateTime start, decimal hours, Guid? branchId, CancellationToken ct);
}
