using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetAgentPerformanceReport;

public class GetAgentPerformanceReportQueryHandler(
    IRepository<Ticket> tickets,
    IIdentityUserService identityUsers,
    IMessageFactory messages)
    : IQueryHandler<GetAgentPerformanceReportQuery, Response<AgentPerformanceReportDto>>
{
    private static readonly string[] ResolvedStatuses = ["Resolved", "Closed"];

    public async Task<Response<AgentPerformanceReportDto>> Handle(GetAgentPerformanceReportQuery request, CancellationToken ct)
    {
        var resolved = await tickets.ListProjectedAsync(
            t => t.AssigneeId != null && ResolvedStatuses.Contains(t.Status)
                && t.CreatedAt >= request.From && t.CreatedAt <= request.To,
            t => new { t.AssigneeId, t.CreatedAt, t.UpdatedAt },
            ct);

        var byAgent = new List<AgentPerformanceRow>();

        foreach (var group in resolved.GroupBy(t => t.AssigneeId!.Value))
        {
            var agent = await identityUsers.FindByIdAsync(group.Key, ct);
            var rows = group.ToList();

            // Approximation (spec A7): UpdatedAt is the LAST change to the ticket, not necessarily
            // the moment it first reached Resolved. A ticket resolved, reopened, then resolved
            // again reports a longer handle time than the first resolution actually took.
            var avgMinutes = rows.Average(t => ((t.UpdatedAt ?? t.CreatedAt) - t.CreatedAt).TotalMinutes);

            byAgent.Add(new AgentPerformanceRow(group.Key, agent?.FullName ?? string.Empty, rows.Count, avgMinutes));
        }

        return messages.Success(
            new AgentPerformanceReportDto(byAgent.OrderByDescending(r => r.TicketsResolved).ToList()),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
