using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Dtos;

namespace CustomerSupport.Application.Features.Organisation.Queries.GetTeamById;

public record GetTeamByIdQuery(Guid Id) : IQuery<Response<TeamDto>>;
