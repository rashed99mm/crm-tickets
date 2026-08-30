using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateBusinessHoursCalendar;

/// <summary>US-215, AC-228 — records a working window for one weekday in one branch. Day-of-week
/// and times travel as strings so the validator can reject an unparseable value with a field-keyed
/// 400, mirroring how <c>CreateSLAPolicyCommand</c> carries its request values.</summary>
public record CreateBusinessHoursCalendarCommand(
    Guid BranchId, string DayOfWeek, string OpenTime, string CloseTime)
    : ICommand<Response<Guid>>;

public record CreateBusinessHoursCalendarRequest(
    Guid BranchId, string DayOfWeek, string OpenTime, string CloseTime);
