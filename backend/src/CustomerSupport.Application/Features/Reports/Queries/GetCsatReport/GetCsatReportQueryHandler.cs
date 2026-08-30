using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Reports.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Survey;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Reports.Queries.GetCsatReport;

/// <summary>
/// US-605 (reopened by sprint storydept) — customer satisfaction over a period, computed from the
/// survey responses the portal collects on resolved tickets (US-408/409). Matched on when the
/// response was submitted, not when the ticket was created: the survey is the measured event.
/// </summary>
public class GetCsatReportQueryHandler(
    IRepository<SurveyResponse> surveys,
    IMessageFactory messages)
    : IQueryHandler<GetCsatReportQuery, Response<CsatReportDto>>
{
    public async Task<Response<CsatReportDto>> Handle(GetCsatReportQuery request, CancellationToken ct)
    {
        var ratings = await surveys.ListProjectedAsync(
            s => s.CreatedAt >= request.From && s.CreatedAt <= request.To,
            s => new { s.Rating },
            ct);

        var total = ratings.Count;
        var byRating = ratings
            .GroupBy(r => r.Rating)
            .Select(g => new CsatBucket(g.Key, g.Count()))
            .OrderBy(b => b.Rating)
            .ToList();

        var dto = new CsatReportDto(
            TotalResponses: total,
            AverageRating: total == 0 ? 0 : Math.Round(ratings.Average(r => (double)r.Rating), 2),
            Promoters: ratings.Count(r => r.Rating >= 4),
            Passives: ratings.Count(r => r.Rating == 3),
            Detractors: ratings.Count(r => r.Rating <= 2),
            ByRating: byRating);

        return messages.Success(dto, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
