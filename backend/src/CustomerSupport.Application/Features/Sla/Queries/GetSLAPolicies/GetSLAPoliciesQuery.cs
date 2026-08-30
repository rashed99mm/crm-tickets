using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Sla.Queries.GetSLAPolicies;

/// <summary>The paged SLA policy list — AC-127.</summary>
public class GetSLAPoliciesQuery : BasePagedQuery, IQuery<Response<PaginatedList<SLAPolicyDto>>>
{
}
