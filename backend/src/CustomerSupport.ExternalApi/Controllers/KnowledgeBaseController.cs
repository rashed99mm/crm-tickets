using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Application.Features.Contents.Queries.GetContentById;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Features.Contents.Queries.GetContents;
using CustomerSupport.Application.Features.Contents.Queries.GetFaqContents;
using CustomerSupport.Application.Features.Contents.Commands.VoteOnContent;
using CustomerSupport.Application.Features.ContentCategories.Queries.GetContentCategoryTree;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// The customer-facing knowledge base: published help articles only.
/// </summary>
[ApiController]
[Route("api/knowledge-base")]
[ApiVersion("1.0")]
[Produces("application/json")]
[AllowAnonymous]
// Auth note: read endpoints are publicly accessible (AC-144.1 allows anonymous reads).
// A valid X-Api-Key authenticates the caller as a machine client via ApiKeyAuthenticationHandler,
// but unauthenticated requests are also allowed. Vote requires a real customer/staff identity
// so uses [Authorize(AuthenticationSchemes = "Bearer")] only.
public class KnowledgeBaseController : ControllerBase
{
    /// <summary>Status a content item must carry to be visible to customers.</summary>
    private const string PublishedStatus = "Published";

    private readonly IMediator _mediator;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(IMediator mediator, ILogger<KnowledgeBaseController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Lists published help articles, optionally narrowed to a category.</summary>
    [HttpGet("articles")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<PaginatedList<ContentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<PaginatedList<ContentDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Public knowledge base list requested");

        var result = await _mediator.Send(
            new GetContentsQuery
            {
                PageIndex = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Status = PublishedStatus,
                CategoryId = categoryId,
            },
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>The KB category tree, active roots only — backed by the same MediatR query
    /// the staff admin screen uses. Anonymous: the customer-facing KB page needs it.</summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<IReadOnlyList<ContentCategoryNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetContentCategoryTreeQuery(), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Retrieves one published help article.</summary>
    [HttpGet("articles/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<ContentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<ContentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetContentByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// FAQ articles only, distinct from the full list. Paginated, supports search.
    /// Defaults: skip=0, take=3 (bento layout). Pass searchTerm to filter by title/body.
    /// </summary>
    [HttpGet("articles/faq")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Response<PaginatedList<ContentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaqArticles(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 3,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFaqContentsQuery(searchTerm, skip, take), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Vote helpful/unhelpful on a published article. Requires an authenticated caller
    /// (customer session or staff JWT). API key is not accepted for voting.
    /// </summary>
    [HttpPost("articles/{id:guid}/vote")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteOnContentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new VoteOnContentCommand(id, request.IsHelpful), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Grounded question answering over published articles only.
    /// </summary>
    [HttpPost("ask")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask(
        [FromBody] AskKnowledgeBaseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AskKnowledgeBaseCommand(request.Question), ct);
        return this.ToActionResult(result);
    }
}

/// <summary>Request shape for the QA ask endpoint.</summary>
public record AskKnowledgeBaseRequest(string Question);

/// <summary>Request shape for Vote.</summary>
public record VoteOnContentRequest(bool IsHelpful);
