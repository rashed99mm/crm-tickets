using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetAssignableAgents;

public class GetAssignableAgentsQueryHandler(IIdentityUserService identityUsers)
    : IQueryHandler<GetAssignableAgentsQuery, Response<IReadOnlyList<AssignableAgentDto>>>
{
    public async Task<Response<IReadOnlyList<AssignableAgentDto>>> Handle(GetAssignableAgentsQuery request, CancellationToken ct)
    {
        var agents = await identityUsers.GetUsersInRoleAsync(ApplicationRole.Roles.Agent, ct);

        IReadOnlyList<AssignableAgentDto> options =
            [.. agents.Select(a => new AssignableAgentDto(a.Id, a.FullName, a.Email ?? string.Empty))];

        return Response<IReadOnlyList<AssignableAgentDto>>.Ok(options, SystemCodeMap.Resolve("SUCCESS_OPERATION"), "OK");
    }
}
