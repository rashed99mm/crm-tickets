using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.TakeEscalation;

/// <summary>
/// Hands an escalated ticket to a named Specialist/Supervisor — US-904, AC-506.
/// </summary>
public record TakeEscalationCommand(Guid TicketId, Guid AssigneeId, string RowVersion)
    : ICommand<Response<Guid>>;

/// <summary>The escalation hand-off payload — US-904, AC-506.</summary>
public record TakeEscalationRequest(Guid AssigneeId, string RowVersion);
