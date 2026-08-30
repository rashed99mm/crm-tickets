namespace CustomerSupport.Application.Features.Organisation.Dtos;

/// <summary>A team as the API returns it — US-905, AC-508.</summary>
public record TeamDto(Guid Id, string Name, Guid DepartmentId, Guid? ManagerId, bool IsActive, DateTime CreatedAt);

/// <summary>The create/update payload.</summary>
public record TeamRequest(string Name, Guid DepartmentId, Guid? ManagerId);
