using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Tickets.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// The fixed ticket category list. Read-only in S1 (assumption A4).
/// </summary>
/// <remarks>
/// Exists because the ticket create form needs a picker, and a form offering free text would let an
/// agent invent a category — which is what <c>BR-14</c> refuses and what reporting could not group
/// by. There is deliberately no create, update or delete: the list is a developer concern until a
/// later slice says otherwise.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists the active ticket categories, alphabetically.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(Response<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoriesQuery(), ct);
        return this.ToActionResult(result);
    }
}
