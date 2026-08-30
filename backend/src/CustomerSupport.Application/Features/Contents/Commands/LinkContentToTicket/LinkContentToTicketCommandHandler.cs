using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Commands.LinkContentToTicket;

public class LinkContentToTicketCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentTicketLink> linkRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<LinkContentToTicketCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(LinkContentToTicketCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, ct);
        if (content == null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        if (!content.IsPublished)
        {
            return messages.Fail<Guid>(ApplicationErrors.Content.NOT_PUBLISHABLE, MessageType.Conflict);
        }

        var link = ContentTicketLink.Create(request.TicketId, request.ContentId, userContext.UserId);
        await linkRepository.AddAsync(link, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.ContentTicketLink.EXISTS, MessageType.Conflict);
        }

        return messages.Success(link.Id, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
