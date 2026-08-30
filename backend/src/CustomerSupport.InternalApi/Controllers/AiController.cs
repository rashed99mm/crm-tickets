using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// FEAT-21 â€” AI drafting assistant (US-704â€¦708). Every endpoint answers suggestions or their
/// lifecycle; none of them sends anything to a customer. The human gate lives in the command
/// handlers, the authorization in the same guards the ticket actions use.
/// </summary>
[ApiController]
[Route("api/Tickets/{ticketId:guid}/ai")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize]
public class AiController(IMediator mediator) : ControllerBase
{
    /// <summary>US-704 â€” generate and store a thread summary suggestion.</summary>
    [HttpPost("summary")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summarise(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SummariseTicketCommand(ticketId), ct));

    /// <summary>US-705 â€” suggest categories drawn from the seeded list.</summary>
    [HttpPost("categories")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestCategories(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SuggestCategoriesCommand(ticketId), ct));

    /// <summary>US-706 â€” draft a customer reply for the composer.</summary>
    [HttpPost("reply")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DraftReply(
        Guid ticketId, [FromBody] DraftReplyRequest? request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new DraftReplyCommand(ticketId, request?.Instruction), ct));

    /// <summary>US-707 â€” published KB articles likely to contain the solution.</summary>
    [HttpPost("solutions")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestSolutions(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SuggestSolutionsCommand(ticketId), ct));

    /// <summary>US-708 â€” accept or reject a Pending suggestion; double-resolve is a 409.</summary>
    [HttpPost("suggestions/{suggestionId:guid}")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(
        Guid ticketId, Guid suggestionId,
        [FromBody] ResolveAiSuggestionRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new ResolveAiSuggestionCommand(ticketId, suggestionId, request.Action, request.EditedPayload), ct));

    /// <summary>US-708 â€” tracking list for the ticket, newest first.</summary>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(Response<AiSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid ticketId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ListAiSuggestionsQuery(ticketId), ct));
}
