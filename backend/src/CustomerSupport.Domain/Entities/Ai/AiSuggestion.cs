using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Ai;

/// <summary>
/// One AI-generated draft (summary, category list, reply, solution list) and its human-gate
/// lifecycle (US-703, US-708). A suggestion never mutates a ticket on creation; only an explicit
/// agent decision moves it out of Pending, and every edit is flagged so acceptance-rate reporting
/// can tell "used verbatim" from "used after editing".
/// </summary>
public class AiSuggestion : BaseEntity
{
    public static readonly string[] AllowedKinds = ["Summary", "Categories", "Reply", "Solutions"];
    public static readonly string[] AllowedStatuses = ["Pending", "Accepted", "Rejected"];

    /// <summary>
    /// AC-21.11 — the three sentiment labels the Context Summary card renders as a chip.
    /// Serialised to JSON as the enum's <em>name</em> so the wire shape is stable across reorders
    /// of the constants; a missing or unparseable value becomes <c>null</c> at the handler
    /// boundary, never a thrown exception.
    /// </summary>
    public enum AiSentiment
    {
        Frustrated,
        Neutral,
        Satisfied,
    }

    public Guid TicketId { get; private set; }
    public string Kind { get; private set; } = string.Empty;

    /// <summary>JSON payload exactly as generated. Never re-rendered server-side.</summary>
    public string Payload { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending";
    public bool Edited { get; private set; }
    public Guid CreatedByActorId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static AiSuggestion Create(
        Guid ticketId, string kind, string payload, Guid actorId)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket is required", nameof(ticketId));
        }

        if (!AllowedKinds.Contains(kind))
        {
            throw new ArgumentException($"Kind must be one of: {string.Join(", ", AllowedKinds)}", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("A payload is required", nameof(payload));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("An actor is required", nameof(actorId));
        }

        return new AiSuggestion
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Kind = kind,
            Payload = payload,
            Status = "Pending",
            Edited = false,
            CreatedByActorId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
        };
    }

    /// <summary>
    /// US-708 — the only transitions are Pending → Accepted and Pending → Rejected. An accepted
    /// or rejected row is closed history: accepting twice, or reviving a rejection, would make the
    /// tracking data mean whatever the caller wanted.
    /// </summary>
    public bool Resolve(string targetStatus, string? editedPayload)
    {
        if (!AllowedStatuses.Contains(targetStatus) || targetStatus == "Pending")
        {
            return false;
        }

        if (Status != "Pending")
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(editedPayload) && editedPayload != Payload)
        {
            Payload = editedPayload;
            Edited = true;
        }

        Status = targetStatus;
        MarkUpdated();
        return true;
    }
}
