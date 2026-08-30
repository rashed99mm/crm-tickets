using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Auth.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent
) : ICommand<Response<AuthResponse>>;
