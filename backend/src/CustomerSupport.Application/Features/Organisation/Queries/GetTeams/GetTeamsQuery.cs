using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetTeams;

public class GetTeamsQuery : BasePagedQuery, IQuery<Response<PaginatedList<TeamDto>>>
{
}
