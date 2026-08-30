namespace CustomerSupport.Application.Features.Organisation.Dtos;

/// <summary>A department as the API returns it — AC-115.</summary>
public record DepartmentDto(Guid Id, string Name, Guid? ManagerId, bool IsActive, DateTime CreatedAt);

/// <summary>The create/update payload — AC-119.</summary>
public record DepartmentRequest(string Name, Guid? ManagerId);
