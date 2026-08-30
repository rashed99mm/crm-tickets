namespace CustomerSupport.Application.Features.Sla.Dtos;

/// <summary>US-215, AC-228 — a branch public holiday as the API returns it.</summary>
public record PublicHolidayDto(Guid Id, Guid BranchId, DateOnly HolidayDate, string Name);
