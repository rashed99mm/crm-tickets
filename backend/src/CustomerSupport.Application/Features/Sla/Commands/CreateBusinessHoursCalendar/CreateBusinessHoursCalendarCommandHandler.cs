using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateBusinessHoursCalendar;

public class CreateBusinessHoursCalendarCommandHandler(
    IRepository<BusinessHoursCalendar> calendars,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CreateBusinessHoursCalendarCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateBusinessHoursCalendarCommand request, CancellationToken ct)
    {
        var calendar = BusinessHoursCalendar.Create(
            request.BranchId,
            Enum.Parse<DayOfWeek>(request.DayOfWeek, ignoreCase: true),
            TimeOnly.Parse(request.OpenTime),
            TimeOnly.Parse(request.CloseTime));

        await calendars.AddAsync(calendar, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(calendar.Id, ApplicationErrors.BusinessHours.CALENDAR_CREATED);
    }
}
