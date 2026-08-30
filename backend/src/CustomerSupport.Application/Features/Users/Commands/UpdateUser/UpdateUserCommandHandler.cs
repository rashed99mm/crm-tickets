using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using MediatR;

namespace CustomerSupport.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler(
    IIdentityUserService identityUserService,
    IMessageFactory messages)
    : ICommandHandler<UpdateUserCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await identityUserService.FindByIdAsync(request.Id, ct);

        if (user is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.User.NOT_FOUND);
        }

        user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber, request.ProfileImageUrl);
        user.AssignOrganisation(request.DepartmentId, request.BranchId, request.TeamId);

        var result = await identityUserService.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return messages.Fail<Guid>(string.Join(", ", result.Errors), MessageType.Internal);
        }

        return messages.Success(user.Id, ApplicationErrors.User.UPDATED);
    }
}
