using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Ai.Chat;
using CustomerSupport.Domain.Entities.Ai;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// AI-38..AI-42 — staff multi-turn assistant conversations. The scope is set by this host, never
/// the client, so a staff session and a portal session can never be cross-reached (A5, AI-40).
/// </summary>
[ApiController]
[Route("api/ai/chats")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("ai")]
public class AiChatController(IMediator mediator) : ControllerBase
{
    /// <summary>AI-38 — open a session and answer the first message.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start([FromBody] StartAiChatRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new StartAiChatCommand(request.Message, AiChatScope.Staff), ct));

    /// <summary>AI-39 — append a turn to an open session.</summary>
    [HttpPost("{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(Guid sessionId, [FromBody] SendAiChatMessageRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new SendAiChatMessageCommand(sessionId, request.Message, AiChatScope.Staff), ct));

    /// <summary>Load a session transcript.</summary>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new GetAiChatQuery(sessionId, AiChatScope.Staff), ct));

    /// <summary>AI-42 — create a ticket from the transcript and close the session.</summary>
    [HttpPost("{sessionId:guid}/handoff")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Handoff(
        Guid sessionId, [FromBody] HandoffFromChatRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new HandoffFromChatCommand(sessionId, request.CustomerId, request.CategoryId, AiChatScope.Staff), ct));
}
