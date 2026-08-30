using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Sla.Commands.CreateSLAPolicy;
using CustomerSupport.Application.Features.Sla.Commands.DeactivateSLAPolicy;
using CustomerSupport.Application.Features.Sla.Commands.UpdateSLAPolicy;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Application.Features.Sla.Queries.GetSLAPolicies;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// SLA policies — FEAT-17, `AC-124`..`AC-127`, plus full CRUD (`US-214`).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class SLAPoliciesController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists SLA policies, paginated.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Rows per page. Above the server maximum this is a 400 (AC-11).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<SLAPolicyDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<SLAPolicyDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSLAPoliciesQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    /// <summary>Defines an SLA target for a priority. Admin only (AC-125).</summary>
    /// <param name="request">Priority, response/resolution target hours, and optional category/branch scope.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateSLAPolicyRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateSLAPolicyCommand(
                request.Priority, request.ResponseTargetHours, request.ResolutionTargetHours,
                request.CategoryId, request.BranchId),
            ct);

        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>Corrects an SLA policy. Admin only.</summary>
    /// <param name="id">The policy identifier.</param>
    /// <param name="request">The corrected values.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateSLAPolicyRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateSLAPolicyCommand(
                id, request.Priority, request.ResponseTargetHours, request.ResolutionTargetHours,
                request.CategoryId, request.BranchId),
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>Deactivates an SLA policy (soft-delete). Admin only.</summary>
    /// <remarks>Answers 200 with the envelope, not 204 — matching every other deactivate route in
    /// this codebase (<c>DepartmentsController.Delete</c>, <c>CustomersController.Delete</c>).</remarks>
    /// <param name="id">The policy identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateSLAPolicyCommand(id), ct);
        return this.ToActionResult(result);
    }
}
