using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetDepartmentById;

public record GetDepartmentByIdQuery(Guid Id) : IQuery<Response<DepartmentDto>>;
