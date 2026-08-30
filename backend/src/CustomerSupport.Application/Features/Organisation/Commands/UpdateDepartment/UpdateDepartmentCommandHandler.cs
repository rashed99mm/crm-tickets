using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateDepartment;

public class UpdateDepartmentCommandHandler(
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<UpdateDepartmentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var department = await departments.GetTrackedAsync(request.Id, ct);
        if (department is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Department.NOT_FOUND);
        }

        department.Update(request.Name, request.ManagerId);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Department.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(department.Id, ApplicationErrors.Department.UPDATED);
    }
}
