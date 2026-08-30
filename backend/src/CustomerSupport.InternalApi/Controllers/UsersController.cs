using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Application.Features.Users.Commands.ActivateUser;
using CustomerSupport.Application.Features.Users.Commands.AssignRoles;
using CustomerSupport.Application.Features.Users.Commands.CreateUser;
using CustomerSupport.Application.Features.Users.Commands.DeactivateUser;
using CustomerSupport.Application.Features.Users.Commands.DeleteUser;
using CustomerSupport.Application.Features.Users.Commands.UpdateUser;
using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Application.Features.Users.Queries.GetUserById;
using CustomerSupport.Application.Features.Users.Queries.GetUsers;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Manages user accounts, roles, and activation state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, ILogger<UsersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all users with pagination and filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<UserListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? role = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("User list requested");

        var query = new GetUsersQuery
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Search = search,
            IsActive = isActive,
            Role = role
        };
        
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves a specific user by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("User {UserId} requested", id);

        var result = await _mediator.Send(new GetUserByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        _logger.LogInformation("User creation requested");

        var command = new CreateUserCommand(
            request.Email,
            request.Username,
            request.Password,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Roles
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Updates an existing user's profile.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Update requested for user {UserId}", id);

        var command = new UpdateUserCommand(
            id,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.ProfileImageUrl,
            request.DepartmentId,
            request.BranchId,
            request.TeamId
        );
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Permanently deletes a user account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Delete requested for user {UserId}", id);

        var result = await _mediator.Send(new DeleteUserCommand(id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Replaces all roles assigned to a user.
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Role assignment requested for user {UserId}", id);

        var command = new AssignRolesCommand(id, request.Roles);
        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Activates a previously deactivated user account.
    /// </summary>
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Activation requested for user {UserId}", id);

        var result = await _mediator.Send(new ActivateUserCommand(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Deactivates an active user account.
    /// </summary>
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deactivation requested for user {UserId}", id);

        var result = await _mediator.Send(new DeactivateUserCommand(id), ct);
        return this.ToActionResult(result);
    }
}
