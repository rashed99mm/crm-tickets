using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetSlaPerformanceReport;

public class GetSlaPerformanceReportQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<SLAEvent> slaEvents,
    IMessageFactory messages)
    : IQueryHandler<GetSlaPerformanceReportQuery, Response<SlaPerformanceReportDto>>
{
    public async Task<Response<SlaPerformanceReportDto>> Handle(GetSlaPerformanceReportQuery request, CancellationToken ct)
    {
        // Only tickets a policy actually matched (spec A6) — one with no due date was never on an
        // SLA clock and has nothing to report.
        var withTargets = await tickets.ListProjectedAsync(
            t => t.CreatedAt >= request.From && t.CreatedAt <= request.To
                && (t.ResponseDueAt != null || t.ResolutionDueAt != null),
            t => new { t.Id, t.Priority, t.ResponseDueAt, t.ResolutionDueAt },
            ct);

        var ticketIds = withTargets.Select(t => t.Id).ToList();

        var breaches = await slaEvents.ListAsync(e => ticketIds.Contains(e.TicketId) && e.BreachedAt != null, ct);
        var breachedResponse = breaches.Where(e => e.TargetType == SLAEvent.TargetTypes.Response)
            .Select(e => e.TicketId).ToHashSet();
        var breachedResolution = breaches.Where(e => e.TargetType == SLAEvent.TargetTypes.Resolution)
            .Select(e => e.TicketId).ToHashSet();

        var byPriority = withTargets
            .GroupBy(t => t.Priority)
            .Select(g =>
            {
                var withResponseTarget = g.Where(t => t.ResponseDueAt != null).ToList();
                var withResolutionTarget = g.Where(t => t.ResolutionDueAt != null).ToList();
                var breachedResponseCount = withResponseTarget.Count(t => breachedResponse.Contains(t.Id));
                var breachedResolutionCount = withResolutionTarget.Count(t => breachedResolution.Contains(t.Id));

                return new SlaPerformanceRow(
                    g.Key,
                    g.Count(),
                    MetFirstResponse: withResponseTarget.Count - breachedResponseCount,
                    BreachedFirstResponse: breachedResponseCount,
                    MetResolution: withResolutionTarget.Count - breachedResolutionCount,
                    BreachedResolution: breachedResolutionCount);
            })
            .OrderBy(r => r.Priority)
            .ToList();

        return messages.Success(new SlaPerformanceReportDto(byPriority), ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
