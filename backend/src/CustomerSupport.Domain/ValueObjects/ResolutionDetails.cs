namespace CustomerSupport.Domain.ValueObjects;

/// <summary>
/// What the resolver must state when a ticket enters <c>Resolved</c> (US-922). Validated inside
/// <c>Ticket.ChangeStatus</c> — this record is the carrier, not the rule.
/// </summary>
public sealed record ResolutionDetails(string Code, string Notes);
