namespace CustomerSupport.Application.Features.Users.Dtos;

public record UserDto(
    Guid Id,
    string Email,
    string Username,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles
);

public record UserListItemDto(
    Guid Id,
    string Email,
    string Username,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles
);

public record CreateUserDto(
    string Email,
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyList<string>? Roles
);

public record UpdateUserDto(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? ProfileImageUrl
);
