using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.CreateExternalApiConfiguration;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.DeleteExternalApiConfiguration;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.DisableExternalApiConfiguration;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.EnableExternalApiConfiguration;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiAuth;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Commands.UpdateExternalApiConfiguration;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Dtos;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurationByName;
using CustomerSupport.Application.Features.ExternalApiConfigurations.Queries.GetExternalApiConfigurations;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

[ApiController]
[Route("api/externalapi-configs")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ExternalApiConfigurationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExternalApiConfigurationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<ExternalApiConfigurationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = new GetExternalApiConfigurationsQuery
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Search = search
        };

        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Response<ExternalApiConfigurationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByName(string name, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExternalApiConfigurationByNameQuery(name), ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateExternalApiConfigurationRequest request, CancellationToken ct)
    {
        var command = new CreateExternalApiConfigurationCommand(
            request.Name,
            request.BaseUrl,
            request.TimeoutSeconds,
            request.AuthType,
            request.AuthKeyName,
            request.AuthKeyLocation,
            request.AuthValue,
            request.AuthToken,
            request.AuthTokenUrl,
            request.AuthClientId,
            request.AuthClientSecret,
            request.AuthScope,
            request.AuthAutoRefresh
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    [HttpPut("{name}")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string name, [FromBody] UpdateExternalApiConfigurationRequest request, CancellationToken ct)
    {
        var command = new UpdateExternalApiConfigurationCommand(
            name,
            request.BaseUrl,
            request.TimeoutSeconds
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    [HttpPatch("{name}/auth")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAuth(string name, [FromBody] UpdateExternalApiAuthRequest request, CancellationToken ct)
    {
        var command = new UpdateExternalApiAuthCommand(
            name,
            request.AuthType,
            request.AuthKeyName,
            request.AuthKeyLocation,
            request.AuthValue,
            request.AuthToken,
            request.AuthTokenUrl,
            request.AuthClientId,
            request.AuthClientSecret,
            request.AuthScope,
            request.AuthAutoRefresh
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    [HttpPatch("{name}/enable")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable(string name, CancellationToken ct)
    {
        var result = await _mediator.Send(new EnableExternalApiConfigurationCommand(name), ct);
        return this.ToActionResult(result);
    }

    [HttpPatch("{name}/disable")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(string name, CancellationToken ct)
    {
        var result = await _mediator.Send(new DisableExternalApiConfigurationCommand(name), ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteExternalApiConfigurationCommand(name), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }
}
