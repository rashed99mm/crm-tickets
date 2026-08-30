using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Commands.CreateTicket;

public record CreateTicketCommand(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,
    string Priority,

    /// <summary>The channel the ticket originated on. <c>Portal</c> for the customer-facing host.
    /// Null from the staff surface — an agent-authored ticket carries no source (PJ-5/US-404).</summary>
    string? Source = null) : ICommand<Response<Guid>>;

/// <summary>The create payload — AC-29, AC-30, AC-31.</summary>
public record CreateTicketRequest(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid CategoryId,
    string Priority);
