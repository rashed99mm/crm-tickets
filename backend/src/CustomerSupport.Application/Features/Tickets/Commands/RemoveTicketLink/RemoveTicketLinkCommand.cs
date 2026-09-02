using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.RemoveTicketLink;

/// <summary>Removes one link by id (US-925, AC-925.4). Either endpoint of the link may remove it.</summary>
public record RemoveTicketLinkCommand(Guid TicketId, Guid LinkId) : ICommand<Response<Guid>>;
