using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Auth.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.UpdateCurrentUserProfile;

/// <summary>
/// Updates the signed-in user's own profile. <see cref="FirstName"/>, <see cref="LastName"/>,
/// <see cref="PhoneNumber"/> and <see cref="ProfileImageUrl"/> come from the request; the target
/// user id is taken from the authenticated token by the handler, never from the body.
/// </summary>
public record UpdateCurrentUserProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? ProfileImageUrl)
    : ICommand<Response<UserInfoDto>>;
