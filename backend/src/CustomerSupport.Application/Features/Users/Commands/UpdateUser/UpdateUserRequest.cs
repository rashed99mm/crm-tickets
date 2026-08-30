namespace CustomerSupport.Application.Features.Users.Commands.UpdateUser;

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? ProfileImageUrl,
    Guid? DepartmentId,
    Guid? BranchId,
    Guid? TeamId);
