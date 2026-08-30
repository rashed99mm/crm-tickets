using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateTeam;

public class CreateTeamCommandHandler(
    IRepository<Team> teams,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateTeamCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateTeamCommand request, CancellationToken ct)
    {
        var team = Team.Create(request.Name, request.DepartmentId, request.ManagerId);

        await teams.AddAsync(team, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Team.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(team.Id, ApplicationErrors.Team.CREATED);
    }
}
