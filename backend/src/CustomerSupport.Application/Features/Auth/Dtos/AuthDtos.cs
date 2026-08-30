namespace CustomerSupport.Application.Features.Auth.Dtos;

public record AuthResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    IReadOnlyList<string> Roles
);

public record UserInfoDto(
    Guid Id,
    string Email,
    string Username,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles
);
