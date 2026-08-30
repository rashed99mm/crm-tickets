using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateDepartment;

public class CreateDepartmentCommandHandler(
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateDepartmentCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var department = Department.Create(request.Name, request.ManagerId);

        await departments.AddAsync(department, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Department.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(department.Id, ApplicationErrors.Department.CREATED);
    }
}
