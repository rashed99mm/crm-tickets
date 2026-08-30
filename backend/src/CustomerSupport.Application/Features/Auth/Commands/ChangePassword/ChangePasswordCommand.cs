using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Commands.ChangePassword;

/// <summary>
/// Changes the signed-in user's own password. <see cref="UserId"/> is taken from the
/// authenticated token by the controller, never from the request body.
/// </summary>
public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword)
    : ICommand<Response<Unit>>;
