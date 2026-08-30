using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Sla.Queries.GetPublicHolidays;

/// <summary>US-215, AC-228 — the paged public-holiday list for branches.</summary>
public class GetPublicHolidaysQuery : BasePagedQuery, IQuery<Response<PaginatedList<PublicHolidayDto>>>
{
}
