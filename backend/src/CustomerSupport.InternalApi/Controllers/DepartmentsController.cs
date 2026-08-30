using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Commands.CreateDepartment;
using CustomerSupport.Application.Features.Organisation.Commands.DeactivateDepartment;
using CustomerSupport.Application.Features.Organisation.Commands.UpdateDepartment;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Features.Organisation.Queries.GetDepartmentById;
using CustomerSupport.Application.Features.Organisation.Queries.GetDepartments;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Departments — FEAT-16, AC-115, AC-117..AC-120. Grouping only this sprint; whether a department
/// also restricts visibility is not this feature's question (see BranchesController's remark).
/// </summary>
/// <remarks>
/// Reads require only a session; every mutation is Admin-only (AC-120) — the same split
/// `TicketsController` uses between its open reads and `Assign`'s `Supervisor` gate.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class DepartmentsController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists departments, paginated.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<DepartmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<DepartmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetDepartmentsQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Retrieves one department. An unknown or deactivated id is a 404.</summary>
    /// <param name="id">The department identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<DepartmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDepartmentByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a new department. Admin only (AC-120).</summary>
    /// <param name="request">The name and an optional manager.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] DepartmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateDepartmentCommand(request.Name, request.ManagerId), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>Corrects a department. Admin only (AC-120).</summary>
    /// <param name="id">The department identifier.</param>
    /// <param name="request">The corrected values.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateDepartmentCommand(id, request.Name, request.ManagerId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Deactivates a department (soft-delete). Admin only (AC-120).</summary>
    /// <remarks>Answers 200 with the envelope, not 204, matching this codebase's own convention
    /// (<c>CustomersController.Delete</c>) — a bare 204 carries no code or bilingual message.</remarks>
    /// <param name="id">The department identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateDepartmentCommand(id), ct);
        return this.ToActionResult(result);
    }
}
