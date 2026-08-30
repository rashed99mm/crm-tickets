using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.CreateUser;

public record CreateUserCommand(
    string Email,
    string Username,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    IReadOnlyList<string>? Roles
) : ICommand<Response<Guid>>;
