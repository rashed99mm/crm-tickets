using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Sla.Commands.CreatePublicHoliday;

/// <summary>US-215, AC-228 — records a whole-day exclusion for one branch. The date travels as a
/// string so the validator can reject an unparseable value with a field-keyed 400.</summary>
public record CreatePublicHolidayCommand(Guid BranchId, string HolidayDate, string Name)
    : ICommand<Response<Guid>>;

/// <summary>US-215, AC-228 — the request surface for creating a public holiday.</summary>
public record CreatePublicHolidayRequest(Guid BranchId, string HolidayDate, string Name);
