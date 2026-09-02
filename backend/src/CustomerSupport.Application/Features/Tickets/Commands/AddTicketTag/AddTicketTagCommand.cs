using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.AddTicketTag;

/// <summary>Adds one tag to a ticket (US-924, AC-924.1).</summary>
public record AddTicketTagCommand(Guid TicketId, string Value) : ICommand<Response<Guid>>;

/// <summary>The add-tag payload. The raw value — normalization is the domain's.</summary>
public record AddTicketTagRequest(string Value);
