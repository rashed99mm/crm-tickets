namespace CustomerSupport.Application.Features.Users.Commands.CreateUser;

public record CreateUserRequest(
    string Email,
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyList<string>? Roles
);
