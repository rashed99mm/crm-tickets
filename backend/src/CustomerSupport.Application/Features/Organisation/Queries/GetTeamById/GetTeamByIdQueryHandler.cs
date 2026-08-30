using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetTeamById;

public class GetTeamByIdQueryHandler(
    IRepository<Team> teams,
    IMessageFactory messages)
    : IQueryHandler<GetTeamByIdQuery, Response<TeamDto>>
{
    public async Task<Response<TeamDto>> Handle(GetTeamByIdQuery request, CancellationToken ct)
    {
        var team = await teams.GetByIdAsync(request.Id, ct);
        if (team is null)
        {
            return messages.NotFound<TeamDto>(ApplicationErrors.Team.NOT_FOUND);
        }

        return messages.Success(
            new TeamDto(team.Id, team.Name, team.DepartmentId, team.ManagerId, team.IsActive, team.CreatedAt),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
