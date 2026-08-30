using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.UnlinkContentFromTicket;

public class UnlinkContentFromTicketCommandHandler(
    IRepository<ContentTicketLink> linkRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<UnlinkContentFromTicketCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(UnlinkContentFromTicketCommand request, CancellationToken ct)
    {
        var link = await linkRepository.FirstOrDefaultAsync(
            l => l.TicketId == request.TicketId && l.ContentId == request.ContentId, ct);

        if (link == null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.ContentTicketLink.NOT_FOUND);
        }

        linkRepository.Remove(link);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
