using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetDepartments;

/// <summary>The paged department list — AC-119.</summary>
public class GetDepartmentsQuery : BasePagedQuery, IQuery<Response<PaginatedList<DepartmentDto>>>
{
}
