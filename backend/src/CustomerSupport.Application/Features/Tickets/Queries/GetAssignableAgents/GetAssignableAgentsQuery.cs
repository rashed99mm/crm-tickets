using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Tickets.Queries.GetAssignableAgents;

public record AssignableAgentDto(Guid Id, string Name, string Email);

public record GetAssignableAgentsQuery : IQuery<Response<IReadOnlyList<AssignableAgentDto>>>;
