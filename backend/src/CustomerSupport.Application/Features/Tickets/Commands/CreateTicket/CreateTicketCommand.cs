using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;

public record CreateTicketCommand(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,

    /// <summary>The matrix inputs (US-923). Required from the staff surface; customer-origin
    /// callers (portal, channels, AI handover) omit them and the handler defaults both to
    /// Medium — deriving Normal, the old default priority (spec A2).</summary>
    string? Impact = null,
    string? Urgency = null,

    /// <summary>The channel the ticket originated on. <c>Portal</c> for the customer-facing host.
    /// Null from the staff surface — an agent-authored ticket carries no source (PJ-5/US-404).</summary>
    string? Source = null) : ICommand<Response<Guid>>;

/// <summary>The create payload — AC-29, AC-30, AC-923.1. Priority is not accepted (spec A10).</summary>
public record CreateTicketRequest(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,
    string Impact,
    string Urgency);
