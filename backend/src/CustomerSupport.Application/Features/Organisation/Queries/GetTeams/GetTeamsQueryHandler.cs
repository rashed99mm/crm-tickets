using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetTeams;

public class GetTeamsQueryHandler(
    IRepository<Team> teams,
    IMessageFactory messages)
    : IQueryHandler<GetTeamsQuery, Response<PaginatedList<TeamDto>>>
{
    public async Task<Response<PaginatedList<TeamDto>>> Handle(GetTeamsQuery request, CancellationToken ct)
    {
        var page = await teams.GetPagedAsync(
            request,
            filter: null,
            d => new TeamDto(d.Id, d.Name, d.DepartmentId, d.ManagerId, d.IsActive, d.CreatedAt),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
