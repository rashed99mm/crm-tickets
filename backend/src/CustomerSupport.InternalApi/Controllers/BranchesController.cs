using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Commands.CreateBranch;
using CustomerSupport.Application.Features.Organisation.Commands.DeactivateBranch;
using CustomerSupport.Application.Features.Organisation.Commands.UpdateBranch;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Features.Organisation.Queries.GetBranchById;
using CustomerSupport.Application.Features.Organisation.Queries.GetBranches;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Branches — FEAT-16, AC-116, AC-117, AC-120, AC-123.
/// </summary>
/// <remarks>
/// Grouping only this sprint (spec A1). Whether a branch also *restricts visibility* is `OQ-5`,
/// unresolved at the product level — this controller does not decide it. `BranchId` lands on
/// `Users`, `Tickets` and `Customers` so a future decision has something to filter on; nothing here
/// changes what any existing query returns.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class BranchesController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists branches, paginated.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<BranchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<BranchDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBranchesQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Retrieves one branch. An unknown or deactivated id is a 404.</summary>
    /// <param name="id">The branch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<BranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<BranchDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBranchByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Records a new branch. Admin only (AC-120).</summary>
    /// <param name="request">The name, an optional region, and a timezone (defaults to UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] BranchRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateBranchCommand(request.Name, request.Region, request.Timezone), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>Corrects a branch. Admin only (AC-120).</summary>
    /// <param name="id">The branch identifier.</param>
    /// <param name="request">The corrected values.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] BranchRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBranchCommand(id, request.Name, request.Region, request.Timezone), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Deactivates a branch (soft-delete). Admin only (AC-120).</summary>
    /// <remarks>Answers 200 with the envelope, not 204 — see <c>DepartmentsController.Delete</c>.</remarks>
    /// <param name="id">The branch identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateBranchCommand(id), ct);
        return this.ToActionResult(result);
    }
}
