using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Auth.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(
    string AccessToken,
    string RefreshTokenValue,
    string? IpAddress,
    string? UserAgent
) : ICommand<Response<AuthResponse>>;
