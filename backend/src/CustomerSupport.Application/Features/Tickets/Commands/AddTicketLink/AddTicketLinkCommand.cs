using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketLink;

/// <summary>Links this ticket to another by its reference (US-925, AC-925.1).</summary>
public record AddTicketLinkCommand(Guid TicketId, string LinkType, string TargetReference)
    : ICommand<Response<Guid>>;

/// <summary>The add-link payload. The target is addressed by its TKT-nnnnnn reference.</summary>
public record AddTicketLinkRequest(string LinkType, string TargetReference);
