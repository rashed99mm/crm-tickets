using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Application.Features.Contents.Commands.CreateContent;
using CustomerSupport.Application.Features.Contents.Commands.DeleteContent;
using CustomerSupport.Application.Features.Contents.Commands.UpdateContent;
using CustomerSupport.Application.Features.Contents.Commands.PublishContent;
using CustomerSupport.Application.Features.Contents.Commands.ArchiveContent;
using CustomerSupport.Application.Features.Contents.Queries.GetContentVersions;
using CustomerSupport.Application.Features.Contents.Commands.AssignContentCategory;
using CustomerSupport.Application.Features.Contents.Commands.SetFaqFlag;
using ContentDto = CustomerSupport.Application.Features.Contents.Dtos.ContentDto;
using CustomerSupport.Application.Features.Contents.Queries.GetContentById;
using CustomerSupport.Application.Features.Contents.Queries.GetContents;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Contents.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Manages content items such as articles and pages.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class ContentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ContentsController> _logger;

    public ContentsController(IMediator mediator, ILogger<ContentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all content items with pagination and filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Response<PaginatedList<ContentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? authorId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Content list requested");

        var query = new GetContentsQuery
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            SearchTerm = searchTerm,
            Status = status,
            AuthorId = authorId
        };
        
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves a specific content item by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Response<ContentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Content {ContentId} requested", id);

        var result = await _mediator.Send(new GetContentByIdQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Creates a new content item.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateContentRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Content creation requested by user {UserId}", User.GetRequiredUserId());

        var command = new CreateContentCommand(
            request.Title,
            request.Body,
            request.Summary,
            request.ContentType,
            User.GetRequiredUserId(),
            request.Status,
            request.FeaturedImageUrl,
            request.Tags ?? Array.Empty<string>(),
            request.Category,
            request.ExpiresAt,
            request.IsFeatured
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Updates an existing content item.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContentRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Update requested for content {ContentId}", id);

        var command = new UpdateContentCommand(
            id,
            request.Title,
            request.Body,
            request.Summary,
            request.Status,
            request.FeaturedImageUrl,
            request.Tags,
            request.Category,
            request.PublishedAt,
            request.ExpiresAt,
            request.IsFeatured
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Soft-deletes a content item.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Delete requested for content {ContentId}", id);

        var result = await _mediator.Send(new DeleteContentCommand(id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }

    /// <summary>Publishes a Draft article — AC-165, AC-167.</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishContentCommand(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Archives an article — AC-166.</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ArchiveContentCommand(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>An article's version history, newest first — AC-170.</summary>
    [HttpGet("{id:guid}/versions")]
    [Authorize]
    [ProducesResponseType(typeof(Response<IReadOnlyList<ContentVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContentVersionsQuery(id), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Assigns (or clears) an article's category — AC-172.</summary>
    [HttpPut("{id:guid}/category")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCategory(Guid id, [FromBody] AssignContentCategoryRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignContentCategoryCommand(id, request.CategoryId), ct);
        return this.ToActionResult(result);
    }

    /// <summary>Marks or unmarks an article as FAQ — AC-175, AC-176.</summary>
    [HttpPut("{id:guid}/faq")]
    [Authorize]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetFaq(Guid id, [FromBody] SetFaqFlagRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetFaqFlagCommand(id, request.IsFaq), ct);
        return this.ToActionResult(result);
    }
}

/// <summary>Request shape for <see cref="ContentsController.SetFaq"/>.</summary>
public record SetFaqFlagRequest(bool IsFaq);

/// <summary>Request shape for <see cref="ContentsController.AssignCategory"/>.</summary>
public record AssignContentCategoryRequest(Guid? CategoryId);
