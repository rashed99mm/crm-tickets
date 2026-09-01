using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;

/// <summary>
/// Replaces a role's permission set in one transaction (AC-806.1).
///
/// <paramref name="ExpectedPermissionIds"/> is the set the caller staged from. A mismatch against
/// what is stored is refused, never merged (AC-806.5, spec A6) — two administrators editing the
/// same role must not silently overwrite one another.
///
/// Both lists are nullable so an absent JSON property becomes a field-keyed 400 from the validator
/// rather than a NullReferenceException in the handler (AC-806.6).
/// </summary>
public sealed record SetRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyList<Guid>? PermissionIds,
    IReadOnlyList<Guid>? ExpectedPermissionIds) : ICommand<Response<Unit>>;

/// <summary>
/// The request body. <c>RoleId</c> is absent by design — it comes from the route, so there is no
/// second copy that could disagree with it.
/// </summary>
public sealed record SetRolePermissionsRequest(
    IReadOnlyList<Guid>? PermissionIds,
    IReadOnlyList<Guid>? ExpectedPermissionIds);
