using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateTeam;

/// <summary>Records a new team — US-905, AC-508.</summary>
public record CreateTeamCommand(string Name, Guid DepartmentId, Guid? ManagerId) : ICommand<Response<Guid>>;
