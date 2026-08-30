using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;

namespace CustomerSupport.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandler(
    IIdentityUserService identityUserService,
    IMessageFactory messages)
    : ICommandHandler<CreateUserCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (await identityUserService.FindByEmailAsync(request.Email, ct) is not null)
        {
            return messages.Fail<Guid>(ApplicationErrors.User.EMAIL_EXISTS, MessageType.Conflict);
        }

        if (await identityUserService.FindByUsernameAsync(request.Username, ct) is not null)
        {
            return messages.Fail<Guid>(ApplicationErrors.User.USERNAME_EXISTS, MessageType.Conflict);
        }

        var user = ApplicationUser.Create(request.Email, request.Username, request.FirstName, request.LastName);

        var result = await identityUserService.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return messages.Fail<Guid>(string.Join(", ", result.Errors), MessageType.Internal);
        }

        var roles = request.Roles?.Count > 0 ? request.Roles : [ApplicationRole.Roles.User];

        foreach (var role in roles)
        {
            await identityUserService.EnsureRoleExistsAsync(role, role, ct);

            var addRole = await identityUserService.AddToRoleAsync(user, role);

            if (!addRole.Succeeded)
            {
                return messages.Fail<Guid>(string.Join(", ", addRole.Errors), MessageType.Internal);
            }
        }

        return messages.Success(user.Id, ApplicationErrors.User.CREATED);
    }
}
