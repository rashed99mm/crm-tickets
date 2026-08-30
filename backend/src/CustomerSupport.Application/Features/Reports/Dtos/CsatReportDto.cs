namespace CustomerSupport.Application.Features.Reports.Dtos;

/// <summary>One rating value and how many customers gave it (US-605).</summary>
public record CsatBucket(int Rating, int Count);

/// <summary>
/// Customer satisfaction over a period (US-605, AC-155..157 adapted): the average rating, the
/// response count, and an NMS-style promoters/passives/detractors split (4–5 / 3 / 1–2).
/// </summary>
public record CsatReportDto(
    int TotalResponses,
    double AverageRating,
    int Promoters,
    int Passives,
    int Detractors,
    IReadOnlyList<CsatBucket> ByRating);
