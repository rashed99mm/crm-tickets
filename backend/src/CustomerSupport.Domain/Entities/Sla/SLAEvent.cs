using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Sla;

/// <summary>
/// An immutable record of an SLA target's tracking state — currently only breaches (AC-131).
/// Append-only via <see cref="IAppendOnlyEntity"/>, the same guard <c>TicketHistory</c> and
/// <c>TicketMessage</c> already use.
/// </summary>
public class SLAEvent : BaseEntity, IAppendOnlyEntity
{
    public static class TargetTypes
    {
        public const string Response = "Response";
        public const string Resolution = "Resolution";
    }

    public Guid TicketId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public DateTime TargetAt { get; private set; }
    public DateTime? BreachedAt { get; private set; }

    /// <summary>
    /// Not computed this slice (spec A4) — always 0. The column exists for `US-213`'s pause/resume
    /// accounting to fill in once it is built.
    /// </summary>
    public int PausedSeconds { get; private set; }

    public static SLAEvent Record(Guid ticketId, string targetType, DateTime targetAt, DateTime? breachedAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (targetType != TargetTypes.Response && targetType != TargetTypes.Resolution)
        {
            throw new ArgumentException(
                $"TargetType must be one of: {TargetTypes.Response}, {TargetTypes.Resolution}", nameof(targetType));
        }

        return new SLAEvent
        {
            // Id deliberately unassigned — same reason as every other IAppendOnlyEntity here.
            TicketId = ticketId,
            TargetType = targetType,
            TargetAt = targetAt,
            BreachedAt = breachedAt,
            PausedSeconds = 0,
            CreatedAt = DateTime.UtcNow
        };
    }
}
