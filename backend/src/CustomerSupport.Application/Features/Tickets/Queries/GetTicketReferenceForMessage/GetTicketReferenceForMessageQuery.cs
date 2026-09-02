using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetTicketReferenceForMessage;

/// <summary>
/// The human-readable TKT-nnnnnn reference of the ticket a message belongs to (spec A25).
///
/// Exists because the web form has to show the customer a reference the moment they submit, while
/// IngestInboundChannelMessageCommand returns the message id — and widening that shared command's
/// response would change a contract asserted by IngestInboundChannelMessageTests and consumed by
/// three other controllers that do not need it.
/// </summary>
public record GetTicketReferenceForMessageQuery(Guid MessageId) : IQuery<Response<string>>;
