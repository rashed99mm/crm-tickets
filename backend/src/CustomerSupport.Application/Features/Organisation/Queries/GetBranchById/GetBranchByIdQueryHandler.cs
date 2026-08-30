using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetBranchById;

public class GetBranchByIdQueryHandler(
    IRepository<Branch> branches,
    IMessageFactory messages)
    : IQueryHandler<GetBranchByIdQuery, Response<BranchDto>>
{
    public async Task<Response<BranchDto>> Handle(GetBranchByIdQuery request, CancellationToken ct)
    {
        var branch = await branches.GetByIdAsync(request.Id, ct);
        if (branch is null)
        {
            return messages.NotFound<BranchDto>(ApplicationErrors.Branch.NOT_FOUND);
        }

        return messages.Success(
            new BranchDto(branch.Id, branch.Name, branch.Region, branch.Timezone, branch.IsActive, branch.CreatedAt),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
