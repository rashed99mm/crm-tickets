using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Queries.GetBusinessHoursCalendars;

public class GetBusinessHoursCalendarsQueryHandler(
    IRepository<BusinessHoursCalendar> calendars,
    IMessageFactory messages)
    : IQueryHandler<GetBusinessHoursCalendarsQuery, Response<PaginatedList<BusinessHoursCalendarDto>>>
{
    public async Task<Response<PaginatedList<BusinessHoursCalendarDto>>> Handle(
        GetBusinessHoursCalendarsQuery request, CancellationToken ct)
    {
        var page = await calendars.GetPagedAsync(
            request,
            filter: null,
            p => new BusinessHoursCalendarDto(
                p.Id, p.BranchId, p.DayOfWeek.ToString(), p.OpenTime.ToString("HH:mm"), p.CloseTime.ToString("HH:mm")),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
