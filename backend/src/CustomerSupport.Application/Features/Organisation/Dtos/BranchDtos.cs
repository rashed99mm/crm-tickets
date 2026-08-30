namespace CustomerSupport.Application.Features.Organisation.Dtos;

/// <summary>A branch as the API returns it — AC-116.</summary>
public record BranchDto(Guid Id, string Name, string? Region, string Timezone, bool IsActive, DateTime CreatedAt);

/// <summary>The create/update payload — AC-123.</summary>
public record BranchRequest(string Name, string? Region, string? Timezone);
