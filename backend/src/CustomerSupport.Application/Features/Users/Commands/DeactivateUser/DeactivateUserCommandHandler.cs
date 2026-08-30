using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.DeactivateUser;

public class DeactivateUserCommandHandler(
    IIdentityUserService identityUserService,
    IMessageFactory messages)
    : ICommandHandler<DeactivateUserCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var user = await identityUserService.FindByIdAsync(request.Id, ct);

        if (user is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.User.NOT_FOUND);
        }

        user.Deactivate();

        var result = await identityUserService.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return messages.Fail<Unit>(string.Join(", ", result.Errors), MessageType.Internal);
        }

        return messages.Success(Unit.Value, ApplicationErrors.User.UPDATED);
    }
}
