using Microsoft.AspNetCore.Authorization;

namespace CustomerSupport.Api.Shared.Authorization;

public sealed class PermissionRequirement(string permissionName) : IAuthorizationRequirement
{
    public string PermissionName { get; } = permissionName;
}
