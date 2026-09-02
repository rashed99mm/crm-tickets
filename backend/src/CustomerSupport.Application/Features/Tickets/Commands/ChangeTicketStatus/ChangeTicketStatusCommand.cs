using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.ChangeTicketStatus;

/// <summary>
/// Moves a ticket along its lifecycle — AC-37…AC-41, AC-45…AC-47, BASE-12.
///
/// <paramref name="RowVersion"/> is the version the caller read, echoed back. Without it the
/// <c>rowversion</c> column is decoration: each request loads the ticket fresh, so two sequential
/// callers would both see the current value and both succeed, and AC-41's lost update would happen
/// exactly as the criterion forbids.
/// </summary>
public record ChangeTicketStatusCommand(
    Guid TicketId, string Status, string RowVersion,
    string? ResolutionCode = null, string? ResolutionNotes = null)
    : ICommand<Response<Guid>>;

/// <summary>The status-change payload. Resolution fields are required when the target is Resolved (AC-922.1).</summary>
public record ChangeTicketStatusRequest(
    string Status, string RowVersion,
    string? ResolutionCode = null, string? ResolutionNotes = null);
