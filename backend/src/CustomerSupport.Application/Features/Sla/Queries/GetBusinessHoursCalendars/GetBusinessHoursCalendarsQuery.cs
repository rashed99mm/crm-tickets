using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Sla.Queries.GetBusinessHoursCalendars;

/// <summary>US-215, AC-228 — the paged business-hours calendar row list.</summary>
public class GetBusinessHoursCalendarsQuery : BasePagedQuery, IQuery<Response<PaginatedList<BusinessHoursCalendarDto>>>
{
}
