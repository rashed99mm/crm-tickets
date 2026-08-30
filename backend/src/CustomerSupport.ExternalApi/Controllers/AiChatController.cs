using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Ai.Chat;
using CustomerSupport.Domain.Entities.Ai;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>
/// AI-43..AI-45 — the customer-facing assistant on the portal host. The narrow-surface rule
/// (see KnowledgeBaseController) holds: only conversation endpoints live here, scope is Portal by
/// construction, and every route requires the portal bearer token — an anonymous caller gets 401
/// before anything is persisted (AI-44). Rate limited per IP with the tighter external window.
/// </summary>
[ApiController]
[Route("api/ai/chats")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("ai-external")]
public class AiChatController(IMediator mediator) : ControllerBase
{
    /// <summary>AI-43 — open a customer session and answer the first message.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start([FromBody] StartAiChatRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new StartAiChatCommand(request.Message, AiChatScope.Portal), ct));

    /// <summary>AI-39 — append a turn to an open customer session.</summary>
    [HttpPost("{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(Guid sessionId, [FromBody] SendAiChatMessageRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new SendAiChatMessageCommand(sessionId, request.Message, AiChatScope.Portal), ct));

    /// <summary>Load the caller's own transcript.</summary>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(Response<AiChatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new GetAiChatQuery(sessionId, AiChatScope.Portal), ct));

    /// <summary>AI-45 — hand the conversation to a human; the ticket reaches the staff queue.</summary>
    [HttpPost("{sessionId:guid}/handoff")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Handoff(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new HandoffFromChatCommand(sessionId, CustomerId: null, CategoryId: null, AiChatScope.Portal), ct));
}
