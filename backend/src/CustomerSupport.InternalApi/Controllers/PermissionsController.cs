using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Admin.Commands.AssignPermission;
using CustomerSupport.Application.Features.Admin.Commands.RevokePermission;
using CustomerSupport.Application.Features.Admin.Dtos;
using CustomerSupport.Application.Features.Admin.Queries.GetPermissions;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

[ApiController]
[Route("api/admin/permissions")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "UserManagement")]
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Response<PermissionAdministrationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new GetPermissionsQuery(), ct));

    [HttpPost("{roleId:guid}/{permissionId:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Assign(Guid roleId, Guid permissionId, CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new AssignPermissionCommand(roleId, permissionId), ct));

    [HttpDelete("{roleId:guid}/{permissionId:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke(Guid roleId, Guid permissionId, CancellationToken ct)
        => this.ToActionResult(await mediator.Send(new RevokePermissionCommand(roleId, permissionId), ct));
}
