namespace CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;

/// <summary>
/// The self-service profile update body. Intentionally limited to the four fields an authenticated
/// user may change about themselves; there is no <c>id</c>, <c>email</c>, <c>username</c>,
/// <c>roles</c>, <c>isActive</c>, <c>password</c>, <c>departmentId</c> or <c>branchId</c> member,
/// so the binding cannot reach those values (AC-432).
/// </summary>
public record UpdateCurrentUserProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? ProfileImageUrl);
