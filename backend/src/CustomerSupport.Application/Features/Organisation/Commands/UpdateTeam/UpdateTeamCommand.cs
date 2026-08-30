using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateTeam;

/// <summary>Corrects a team — US-905.</summary>
public record UpdateTeamCommand(Guid Id, string Name, Guid? ManagerId) : ICommand<Response<Guid>>;
