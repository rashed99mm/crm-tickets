using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Commands.SetFaqFlag;

public class SetFaqFlagCommandHandler(
    IRepository<Content> contentRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<SetFaqFlagCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(SetFaqFlagCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        if (request.IsFaq)
        {
            try
            {
                content.MarkAsFaq();
            }
            catch (InvalidOperationException)
            {
                // Reuses CONTENT_NOT_PUBLISHABLE (Task 1) rather than minting a third code for
                // the same underlying rule ("this action needs Published status").
                return messages.Fail<Guid>(ApplicationErrors.Content.NOT_PUBLISHABLE, MessageType.Conflict);
            }
        }
        else
        {
            content.UnmarkFaq();
        }

        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(content.Id, ApplicationErrors.Content.UPDATED);
    }
}
