using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Contents.Commands.LinkContentToTicket;

public record LinkContentToTicketCommand(Guid TicketId, Guid ContentId) : ICommand<Response<Guid>>;
