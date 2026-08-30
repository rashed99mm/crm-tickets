using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Contents.Commands.DeleteContent;

public class DeleteContentCommandHandler(
    IRepository<Content> contentRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<DeleteContentCommandHandler> logger)
    : ICommandHandler<DeleteContentCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeleteContentCommand request, CancellationToken ct)
    {
        logger.LogInformation("Deleting content {ContentId}", request.Id);

        var content = await contentRepository.GetByIdAsync(request.Id, ct);
        if (content == null)
        {
            logger.LogWarning("Delete failed — content {ContentId} not found", request.Id);
            return messages.NotFound<Unit>(ApplicationErrors.Content.NOT_FOUND);
        }

        content.SoftDelete();
        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Content {ContentId} deleted successfully", request.Id);

        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_DELETED);
    }
}
