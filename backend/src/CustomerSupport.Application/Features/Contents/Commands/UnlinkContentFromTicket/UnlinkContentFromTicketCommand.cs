using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.UnlinkContentFromTicket;

public record UnlinkContentFromTicketCommand(Guid TicketId, Guid ContentId) : ICommand<Response<Unit>>;
