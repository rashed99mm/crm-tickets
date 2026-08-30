using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetDepartmentById;

public class GetDepartmentByIdQueryHandler(
    IRepository<Department> departments,
    IMessageFactory messages)
    : IQueryHandler<GetDepartmentByIdQuery, Response<DepartmentDto>>
{
    public async Task<Response<DepartmentDto>> Handle(GetDepartmentByIdQuery request, CancellationToken ct)
    {
        var department = await departments.GetByIdAsync(request.Id, ct);
        if (department is null)
        {
            return messages.NotFound<DepartmentDto>(ApplicationErrors.Department.NOT_FOUND);
        }

        return messages.Success(
            new DepartmentDto(department.Id, department.Name, department.ManagerId, department.IsActive, department.CreatedAt),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
