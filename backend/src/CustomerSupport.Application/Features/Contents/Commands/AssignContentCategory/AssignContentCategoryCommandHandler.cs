using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Contents.Commands.AssignContentCategory;

public class AssignContentCategoryCommandHandler(
    IRepository<Content> contentRepository,
    IRepository<ContentCategory> categoryRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<AssignContentCategoryCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(AssignContentCategoryCommand request, CancellationToken ct)
    {
        var content = await contentRepository.GetByIdAsync(request.ContentId, ct);
        if (content == null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Content.NOT_FOUND);
        }

        if (request.CategoryId.HasValue &&
            !await categoryRepository.ExistsAsync(c => c.Id == request.CategoryId.Value, ct))
        {
            return messages.NotFound<Guid>(ApplicationErrors.ContentCategory.NOT_FOUND);
        }

        content.AssignCategory(request.CategoryId);
        contentRepository.Update(content);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(content.Id, ApplicationErrors.Content.UPDATED);
    }
}
