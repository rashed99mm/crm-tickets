using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateDepartment;

public class DeactivateDepartmentCommandHandler(
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<DeactivateDepartmentCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeactivateDepartmentCommand request, CancellationToken ct)
    {
        var department = await departments.GetTrackedAsync(request.Id, ct);
        if (department is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Department.NOT_FOUND);
        }

        department.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.Department.DEACTIVATED);
    }
}
