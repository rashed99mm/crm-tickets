using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Dtos;
using CustomerSupport.Application.Features.Tickets.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// The categories a portal customer may file a ticket against (US-411, PJ-13). A read-only slice of
/// the staff category catalogue, exposed here because the portal submit form needs the picker.
/// </summary>
[ApiController]
[Route("api/Categories")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists the active categories available to portal customers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoriesQuery(), ct);
        return this.ToActionResult(result);
    }
}