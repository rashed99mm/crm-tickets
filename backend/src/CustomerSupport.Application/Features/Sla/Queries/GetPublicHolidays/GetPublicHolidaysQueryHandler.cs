using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Queries.GetPublicHolidays;

public class GetPublicHolidaysQueryHandler(
    IRepository<PublicHoliday> holidays,
    IMessageFactory messages)
    : IQueryHandler<GetPublicHolidaysQuery, Response<PaginatedList<PublicHolidayDto>>>
{
    public async Task<Response<PaginatedList<PublicHolidayDto>>> Handle(
        GetPublicHolidaysQuery request, CancellationToken ct)
    {
        var page = await holidays.GetPagedAsync(
            request,
            filter: null,
            p => new PublicHolidayDto(p.Id, p.BranchId, p.HolidayDate, p.Name),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
