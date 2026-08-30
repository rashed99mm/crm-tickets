using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Organisation.Commands.CreateTeam;
using CustomerSupport.Application.Features.Organisation.Commands.DeactivateTeam;
using CustomerSupport.Application.Features.Organisation.Commands.UpdateTeam;
using CustomerSupport.Application.Features.Organisation.Dtos;
using CustomerSupport.Application.Features.Organisation.Queries.GetTeamById;
using CustomerSupport.Application.Features.Organisation.Queries.GetTeams;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class TeamsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<TeamDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTeamsQuery { PageIndex = page, PageSize = pageSize }, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<TeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<TeamDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTeamByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] TeamRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateTeamCommand(request.Name, request.DepartmentId, request.ManagerId), ct);

        if (!result.Success)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TeamRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateTeamCommand(id, request.Name, request.ManagerId), ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateTeamCommand(id), ct);
        return this.ToActionResult(result);
    }
}
