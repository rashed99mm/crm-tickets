using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Contents.Commands.VoteOnContent;

public class VoteOnContentCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentVote> voteRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messages)
    : ICommandHandler<VoteOnContentCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(VoteOnContentCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, ct);
        if (content == null || !content.IsPublished)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Content.NOT_FOUND);
        }

        var existing = await voteRepository.FirstOrDefaultAsync(
            v => v.ContentId == request.ContentId && v.UserId == userContext.UserId, ct);

        if (existing == null)
        {
            await voteRepository.AddAsync(ContentVote.Create(request.ContentId, userContext.UserId, request.IsHelpful), ct);
            if (request.IsHelpful) content.IncrementLikeCount(); else content.IncrementDislikeCount();
        }
        else if (existing.IsHelpful != request.IsHelpful)
        {
            existing.ChangeTo(request.IsHelpful);
            voteRepository.Update(existing);
            if (request.IsHelpful)
            {
                content.IncrementLikeCount();
                content.DecrementDislikeCount();
            }
            else
            {
                content.IncrementDislikeCount();
                content.DecrementLikeCount();
            }
        }
        // else: same vote resubmitted — idempotent, no count change.

        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);
        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
