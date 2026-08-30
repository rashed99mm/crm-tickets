namespace CustomerSupport.Application.Features.Sla.Dtos;

/// <summary>US-215, AC-228 — a branch working window as the API returns it (day and times as
/// strings, matching the request surface).</summary>
public record BusinessHoursCalendarDto(
    Guid Id, Guid BranchId, string DayOfWeek, string OpenTime, string CloseTime);
