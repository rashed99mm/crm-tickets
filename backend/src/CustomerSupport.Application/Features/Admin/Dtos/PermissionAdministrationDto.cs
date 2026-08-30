namespace CustomerSupport.Application.Features.Admin.Dtos;

public sealed record PermissionAdministrationDto(
    IReadOnlyList<PermissionAdministrationRoleDto> Roles,
    IReadOnlyList<PermissionAdministrationPermissionDto> Permissions);

public sealed record PermissionAdministrationRoleDto(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> PermissionIds);

public sealed record PermissionAdministrationPermissionDto(
    Guid Id,
    string Name,
    string? Description);
