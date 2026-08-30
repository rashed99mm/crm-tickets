using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AssignTicket;

/// <summary>
/// Assigns or reassigns a ticket — AC-42, AC-44, BASE-13, AC-533.
///
/// AC-43 (agents may not assign at all) and AC-533 (self-assign allowed) are both enforced
/// in the handler via <see cref="IUserContext"/>, so the endpoint policy is relaxed to
/// Authenticated and the handler makes the per-call decision.
/// </summary>
public record AssignTicketCommand(Guid TicketId, Guid AssigneeId, string RowVersion)
    : ICommand<Response<Guid>>;

/// <summary>The assignment payload — AC-42, AC-44.</summary>
public record AssignTicketRequest(Guid AssigneeId, string RowVersion);
