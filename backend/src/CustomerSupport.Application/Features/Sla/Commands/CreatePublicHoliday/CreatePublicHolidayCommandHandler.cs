using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.CreatePublicHoliday;

public class CreatePublicHolidayCommandHandler(
    IRepository<PublicHoliday> holidays,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CreatePublicHolidayCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreatePublicHolidayCommand request, CancellationToken ct)
    {
        var holiday = PublicHoliday.Create(
            request.BranchId,
            DateOnly.Parse(request.HolidayDate),
            request.Name);

        await holidays.AddAsync(holiday, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(holiday.Id, ApplicationErrors.BusinessHours.HOLIDAY_CREATED);
    }
}
