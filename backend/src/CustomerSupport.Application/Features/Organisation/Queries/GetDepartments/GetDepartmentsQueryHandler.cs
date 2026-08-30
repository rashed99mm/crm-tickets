using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetDepartments;

public class GetDepartmentsQueryHandler(
    IRepository<Department> departments,
    IMessageFactory messages)
    : IQueryHandler<GetDepartmentsQuery, Response<PaginatedList<DepartmentDto>>>
{
    public async Task<Response<PaginatedList<DepartmentDto>>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var page = await departments.GetPagedAsync(
            request,
            filter: null,
            d => new DepartmentDto(d.Id, d.Name, d.ManagerId, d.IsActive, d.CreatedAt),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
