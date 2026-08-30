using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Commands.PublishContent;

public class PublishContentCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentVersion> contentVersionRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMessageFactory messages,
    ILogger<PublishContentCommandHandler> logger)
    : ICommandHandler<PublishContentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(PublishContentCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        try
        {
            content.Publish();
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Publish refused — content {ContentId} in status {Status}", content.Id, content.Status);
            return messages.Fail<Guid>(ApplicationErrors.Content.NOT_PUBLISHABLE, MessageType.Conflict);
        }

        var snapshot = content.RecordChange("Published");
        var version = ContentVersion.Create(
            content.Id, snapshot.VersionNumber, userContext.UserId, snapshot.ChangeSummary, snapshot.Title, snapshot.Body);
        await contentVersionRepository.AddAsync(version, ct);

        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(content.Id, ApplicationErrors.Content.PUBLISHED);
    }
}
