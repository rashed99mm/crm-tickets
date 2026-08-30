using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateTeam;

public class UpdateTeamCommandHandler(
    IRepository<Team> teams,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<UpdateTeamCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateTeamCommand request, CancellationToken ct)
    {
        var team = await teams.GetTrackedAsync(request.Id, ct);
        if (team is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Team.NOT_FOUND);
        }

        team.Update(request.Name, request.ManagerId);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Team.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(team.Id, ApplicationErrors.Team.UPDATED);
    }
}
