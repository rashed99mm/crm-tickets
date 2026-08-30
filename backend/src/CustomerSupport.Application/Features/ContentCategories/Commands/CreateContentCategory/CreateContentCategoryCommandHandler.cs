using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.ContentCategories.Commands.CreateContentCategory;

public class CreateContentCategoryCommandHandler(
    IRepository<ContentCategory> categoryRepository,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateContentCategoryCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateContentCategoryCommand request, CancellationToken ct)
    {
        // The DB unique index on (Name, ParentId) is SQL Server's default filtered form
        // (`WHERE ParentId IS NOT NULL`) — required so more than one root category can exist at
        // all (a plain unique index would treat every NULL ParentId as equal and cap root
        // categories at one, ever), but it means the index does not protect root-level (ParentId
        // == null) name collisions. Checked explicitly here for that case; non-null-parent
        // collisions are still caught race-safely by the real index below.
        if (request.ParentId is null &&
            await categoryRepository.ExistsAsync(c => c.ParentId == null && c.Name == request.Name.Trim(), ct))
        {
            return messages.Fail<Guid>(ApplicationErrors.ContentCategory.NAME_EXISTS, MessageType.Conflict);
        }

        var category = ContentCategory.Create(request.Name, request.ParentId);

        await categoryRepository.AddAsync(category, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.ContentCategory.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(category.Id, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
