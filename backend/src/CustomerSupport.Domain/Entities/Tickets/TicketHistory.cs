using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Tickets;

/// <summary>
/// One immutable record of something that happened to a ticket (AC-48).
///
/// Append-only (AC-49). Every property has a private setter, there is no mutating method, and
/// <c>AppDbContext.SaveChangesAsync</c> refuses to persist a row of this type in a modified or
/// deleted state via the <see cref="IAppendOnlyEntity"/> guard. See ADR-0010 for why enforcement
/// moved here from the schema.
/// </summary>
public class TicketHistory : BaseEntity, IAppendOnlyEntity
{
    public Guid TicketId { get; private set; }

    /// <summary>Who did it. Taken from the authenticated session, never from a payload (BR-6).</summary>
    public Guid ActorId { get; private set; }

    /// <summary>One of <see cref="TicketChangeType"/>'s five values.</summary>
    public string ChangeType { get; private set; } = string.Empty;

    /// <summary>The previous value, or null when there was none — a creation, or a first assignment.</summary>
    public string? FromValue { get; private set; }

    public string? ToValue { get; private set; }

    /// <summary>
    /// When the business event happened, which is not the same question as when the row was
    /// written — see deviation D5 in the schema design.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Records an event against a ticket. Called by <see cref="Ticket"/>, which is the only place
    /// that knows a change actually took effect.
    /// </summary>
    public static TicketHistory Record(
        Guid ticketId,
        Guid actorId,
        TicketChangeType changeType,
        string? fromValue,
        string? toValue)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        ArgumentNullException.ThrowIfNull(changeType);

        return new TicketHistory
        {
            // Id is deliberately NOT assigned here, and that is not an oversight.
            //
            // When a row is appended to an already-tracked ticket, EF discovers it during change
            // detection and decides Added-versus-Modified by asking whether the primary key is
            // already set. A client-assigned Guid makes it look like an existing row, so EF marks
            // it Modified — and the append-only guard in AppDbContext then refuses the save, on a
            // perfectly legitimate append. Leaving the key unset lets EF mark it Added and generate
            // the Guid itself.
            //
            // The creation path never hit this because there the whole graph is new: an Added
            // parent makes its children Added regardless of their keys.
            TicketId = ticketId,
            ActorId = actorId,
            ChangeType = changeType.Value,
            FromValue = fromValue,
            ToValue = toValue,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId
        };
    }
}
