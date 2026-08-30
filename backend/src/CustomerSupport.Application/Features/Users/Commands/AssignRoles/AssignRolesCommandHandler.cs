using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.AssignRoles;

public class AssignRolesCommandHandler(
    IIdentityUserService identityUserService,
    IMessageFactory messages)
    : ICommandHandler<AssignRolesCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(AssignRolesCommand request, CancellationToken ct)
    {
        var user = await identityUserService.FindByIdAsync(request.UserId, ct);

        if (user is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.User.NOT_FOUND);
        }

        foreach (var role in request.Roles)
        {
            await identityUserService.EnsureRoleExistsAsync(role, role, ct);
        }

        var currentRoles = await identityUserService.GetRolesAsync(user);

        if (currentRoles.Count > 0)
        {
            var removeResult = await identityUserService.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return messages.Fail<Unit>(string.Join(", ", removeResult.Errors), MessageType.Internal);
            }
        }

        var addResult = await identityUserService.AddToRolesAsync(user, request.Roles);
        if (!addResult.Succeeded)
        {
            return messages.Fail<Unit>(string.Join(", ", addResult.Errors), MessageType.Internal);
        }

        return messages.Success(Unit.Value, ApplicationErrors.User.UPDATED);
    }
}
