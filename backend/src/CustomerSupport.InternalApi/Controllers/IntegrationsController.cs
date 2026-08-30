using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Integrations.Commands.ImportCmsErpTickets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>Operational CMS integration actions. Configuration remains in ExternalApiConfigurationsController.</summary>
[ApiController]
[Route("api/integrations")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Admin")]
public sealed class IntegrationsController(IMediator mediator) : ControllerBase
{
    [HttpPost("cms/erp/import-tickets")]
    [ProducesResponseType(typeof(Response<ImportCmsErpTicketsResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ImportErpTickets(CancellationToken ct)
    {
        var result = await mediator.Send(new ImportCmsErpTicketsCommand(), ct);
        return this.ToActionResult(result);
    }
}
