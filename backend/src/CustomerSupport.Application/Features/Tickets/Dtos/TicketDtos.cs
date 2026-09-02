namespace CustomerSupport.Application.Features.Tickets.Dtos;

/// <summary>
/// One row of the queue — AC-32.
///
/// Deliberately carries no description: a list does not need a 4000-character body per row, and
/// shipping one turns a 50-row page into a payload nobody reads.
/// </summary>
public record TicketListItemDto(
    Guid Id,
    string Reference,
    string Subject,
    string Status,
    string Priority,
    Guid CustomerId,
    string CustomerName,
    Guid CategoryId,
    string CategoryName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt,
    // FEAT-17 second slice addendum (2026-08-27), AC-158. None/Warning/Level1/Level2/Level3 (BR-32).
    string EscalationState,
    // US-906 / AC-510. Null until the event happens.
    DateTime? FirstResponseAt,
    DateTime? LastResponseAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    // US-904 / AC-506. The owner of an escalated ticket.
    Guid? EscalationAssigneeId,
    // US-923 / AC-923.6.
    string? Impact,
    string? Urgency,
    // US-924 / AC-924.4. Normalized values, alphabetical; empty when untagged.
    IReadOnlyList<string> Tags);

/// <summary>The full ticket, with its history — AC-35, AC-50.</summary>
public record TicketDetailDto(
    Guid Id,
    string Reference,
    string Subject,
    string Description,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt,
    // The concurrency token, base64 on the wire. The client echoes it back on a status change or
    // an assignment so the server can tell a stale write from a fresh one (AC-41). Opaque by
    // design — nothing but the server interprets it.
    //
    // A plain comment, not `///`: an XML doc comment on a positional record parameter is CS1587.
    string RowVersion,
    CustomerSummaryDto Customer,
    string CategoryName,
    IReadOnlyList<TicketHistoryDto> History,
    // FEAT-17, AC-128/AC-129. Null when no active SLAPolicy matched at creation.
    DateTime? ResponseDueAt,
    DateTime? ResolutionDueAt,
    // FEAT-17, AC-137/AC-138. None/Warning/Level1/Level2/Level3 (BR-32).
    string EscalationState,
    // US-906 / AC-510. Null until the event happens.
    DateTime? FirstResponseAt,
    DateTime? LastResponseAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    // US-904 / AC-506. The owner of an escalated ticket.
    Guid? EscalationAssigneeId,
    // AC-507. The name of the escalation owner, resolved from the AssigneeId.
    string? EscalationAssigneeName,
    // US-922 / AC-922.6. Null / 0 until the ticket has been resolved / reopened.
    string? ResolutionCode,
    string? ResolutionNotes,
    int ReopenCount,
    // US-923 / AC-923.6. Null on tickets created before FEAT-32 (spec A1).
    string? Impact,
    string? Urgency,
    // US-924. Normalized values, alphabetical.
    IReadOnlyList<string> Tags,
    // US-925 / AC-925.5.
    IReadOnlyList<TicketLinkDto> Links);

/// <summary>Enough of the customer to work the ticket without a second request.</summary>
public record CustomerSummaryDto(Guid Id, string Name, string Email, string? Phone);

public record TicketHistoryDto(
    Guid Id,
    string ChangeType,
    string? FromValue,
    string? ToValue,
    Guid ActorId,
    string ActorName,
    DateTime OccurredAt);

/// <summary>
/// One edge of the link graph as seen from the requested ticket (US-925, AC-925.5).
/// <c>Direction</c> is "Outbound" when the requested ticket is the source ("duplicate of ..."),
/// "Inbound" when it is the target ("duplicated by ..." / related-from).
/// </summary>
public record TicketLinkDto(
    Guid Id,
    string LinkType,
    string Direction,
    Guid OtherTicketId,
    string OtherReference,
    string OtherSubject);
