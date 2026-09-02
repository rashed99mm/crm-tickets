using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.ReclassifyTicket;

/// <summary>
/// Sets a ticket's impact/urgency and re-derives its priority (US-923, AC-923.2). RowVersion is
/// echoed for the same lost-update reason as <c>ChangeTicketStatusCommand</c>.
/// </summary>
public record ReclassifyTicketCommand(Guid TicketId, string Impact, string Urgency, string RowVersion)
    : ICommand<Response<Guid>>;

/// <summary>The classification payload. <c>RowVersion</c> is the value read from the detail endpoint.</summary>
public record ReclassifyTicketRequest(string Impact, string Urgency, string RowVersion);
