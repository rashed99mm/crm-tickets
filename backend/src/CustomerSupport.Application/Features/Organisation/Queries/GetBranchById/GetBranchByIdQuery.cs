using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetBranchById;

public record GetBranchByIdQuery(Guid Id) : IQuery<Response<BranchDto>>;
