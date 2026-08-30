using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetTicketVolumeReport;

public class GetTicketVolumeReportQueryHandler(
    IRepository<Ticket> tickets,
    IRepository<Category> categories,
    IMessageFactory messages)
    : IQueryHandler<GetTicketVolumeReportQuery, Response<TicketVolumeReportDto>>
{
    public async Task<Response<TicketVolumeReportDto>> Handle(GetTicketVolumeReportQuery request, CancellationToken ct)
    {
        var rows = await tickets.ListProjectedAsync(
            t => t.CreatedAt >= request.From && t.CreatedAt <= request.To,
            t => new { t.CreatedAt, t.CategoryId, t.Priority },
            ct);

        // Category names are resolved for readability, but grouping stays on CategoryId — two
        // categories could share a display name in principle, and the id is the actual key.
        var categoryIds = rows.Select(r => r.CategoryId).Distinct().ToList();
        var categoryList = await categories.ListAsync(c => categoryIds.Contains(c.Id), ct);
        var categoryNames = categoryList.ToDictionary(c => c.Id, c => c.Name);

        var byPeriod = rows
            .GroupBy(r => PeriodKey(r.CreatedAt, request.GroupBy))
            .Select(g => new ReportBucket(g.Key, g.Count()))
            .OrderBy(b => b.Key)
            .ToList();

        var byCategory = rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new ReportBucket(ResolveCategoryName(g.Key, categoryNames), g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var byPriority = rows
            .GroupBy(r => r.Priority)
            .Select(g => new ReportBucket(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        return messages.Success(
            new TicketVolumeReportDto(byPeriod, byCategory, byPriority),
            ApplicationErrors.General.SUCCESS_OPERATION);
    }

    private static string PeriodKey(DateTime createdAt, string groupBy) => groupBy switch
    {
        "week" => System.Globalization.ISOWeek.GetYear(createdAt) + "-W" +
                  System.Globalization.ISOWeek.GetWeekOfYear(createdAt).ToString("00"),
        "month" => createdAt.ToString("yyyy-MM"),
        _ => createdAt.ToString("yyyy-MM-dd"),
    };

    private static string ResolveCategoryName(Guid categoryId, IReadOnlyDictionary<Guid, string> categoryNames)
    {
        if (categoryNames.TryGetValue(categoryId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return "Uncategorized";
    }
}
