using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateTeam;

/// <summary>Deactivates a team (soft-delete) — US-905, AC-508.</summary>
public record DeactivateTeamCommand(Guid Id) : ICommand<Response<Unit>>;
