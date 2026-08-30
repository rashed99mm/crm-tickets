using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetBranches;

/// <summary>The paged branch list — AC-123.</summary>
public class GetBranchesQuery : BasePagedQuery, IQuery<Response<PaginatedList<BranchDto>>>
{
}
