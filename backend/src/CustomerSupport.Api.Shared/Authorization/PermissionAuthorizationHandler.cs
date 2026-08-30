using CustomerSupport.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CustomerSupport.Api.Shared.Authorization;

public sealed class PermissionAuthorizationHandler(
    IUserContext userContext,
    IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!userContext.IsAuthenticated || userContext.UserId == Guid.Empty)
        {
            return;
        }

        if (await permissionService.HasPermissionAsync(userContext.UserId, requirement.PermissionName))
        {
            context.Succeed(requirement);
        }
    }
}
