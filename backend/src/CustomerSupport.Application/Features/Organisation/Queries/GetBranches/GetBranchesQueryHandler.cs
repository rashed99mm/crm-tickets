using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetBranches;

public class GetBranchesQueryHandler(
    IRepository<Branch> branches,
    IMessageFactory messages)
    : IQueryHandler<GetBranchesQuery, Response<PaginatedList<BranchDto>>>
{
    public async Task<Response<PaginatedList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        var page = await branches.GetPagedAsync(
            request,
            filter: null,
            b => new BranchDto(b.Id, b.Name, b.Region, b.Timezone, b.IsActive, b.CreatedAt),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
