namespace CustomerSupport.Application.Features.Sla.Dtos;

/// <summary>An SLA policy as the API returns it — AC-124.</summary>
public record SLAPolicyDto(
    Guid Id, string Priority, decimal ResponseTargetHours, decimal ResolutionTargetHours,
    Guid? CategoryId, Guid? BranchId, bool IsActive, DateTime CreatedAt);
