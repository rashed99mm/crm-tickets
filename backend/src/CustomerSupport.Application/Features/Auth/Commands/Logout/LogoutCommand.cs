using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.Logout;

public record LogoutCommand(
    string? RefreshToken
) : ICommand<Response<Unit>>;
