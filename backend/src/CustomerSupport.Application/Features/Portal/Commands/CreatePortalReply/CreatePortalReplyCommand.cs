using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Portal.Commands.CreatePortalReply;

/// <summary>A customer replies to their own ticket (US-407, PJ-10).</summary>
public record CreatePortalReplyCommand(
    Guid TicketId,
    string Body,
    Guid CustomerId) : ICommand<Response<Guid>>;