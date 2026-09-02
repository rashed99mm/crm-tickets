using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketTag;

/// <summary>Removes one tag from a ticket (US-924). The value arrives via the route.</summary>
public record RemoveTicketTagCommand(Guid TicketId, string Value) : ICommand<Response<Guid>>;
