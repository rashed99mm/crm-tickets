using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.ContentCategories.Commands.CreateContentCategory;
using CustomerSupport.Application.Features.ContentCategories.Queries.GetContentCategoryTree;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>FEAT-11 — the KB article category taxonomy (AC-171, AC-174). Distinct from the
/// ticket-routing `Category` entity exposed by `CategoriesController`.</summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class ContentCategoriesController(IMediator mediator) : ControllerBase
{
    /// <summary>Creates a category, optionally nested under a parent — AC-171.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateContentCategoryRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateContentCategoryCommand(request.Name, request.ParentId), ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>The full category tree, nested under their parents — AC-174.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<IReadOnlyList<ContentCategoryNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct)
    {
        var result = await mediator.Send(new GetContentCategoryTreeQuery(), ct);
        return this.ToActionResult(result);
    }
}

public record CreateContentCategoryRequest(string Name, Guid? ParentId);
