using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateTeam;

public class DeactivateTeamCommandHandler(
    IRepository<Team> teams,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<DeactivateTeamCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeactivateTeamCommand request, CancellationToken ct)
    {
        var team = await teams.GetTrackedAsync(request.Id, ct);
        if (team is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Team.NOT_FOUND);
        }

        team.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.Team.DEACTIVATED);
    }
}
