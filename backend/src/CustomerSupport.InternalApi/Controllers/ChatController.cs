using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Chat;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.InternalApi.Controllers;

[ApiController]
[Route("api/chat")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "ChatSupport")]
public class ChatController(IMediator mediator) : ControllerBase
{
    [HttpGet("waiting")]
    [ProducesResponseType(typeof(Response<PaginatedList<ChatSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Waiting(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken ct = default) =>
        this.ToActionResult(await mediator.Send(
            new ListWaitingChatSessionsQuery
            {
                PageIndex = page,
                PageSize = pageSize,
                Status = status,
                Search = search,
                SortBy = sortBy,
                SortDirection = sortDirection,
            }, ct));

    [HttpPost("sessions/{sessionId:guid}/claim")]
    [ProducesResponseType(typeof(Response<ChatSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Claim(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ClaimChatSessionCommand(sessionId), ct));

    [HttpGet("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transcript(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new GetChatTranscriptQuery(sessionId), ct));

    [HttpPost("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(Response<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid sessionId, [FromBody] SendLiveChatMessageRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SendAgentChatMessageCommand(sessionId, request.Body), ct));

    [HttpPost("sessions/{sessionId:guid}/close")]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new CloseChatSessionCommand(sessionId), ct));

    [HttpPost("sessions/{sessionId:guid}/ai/reply")]
    [EnableRateLimiting("ai")]
    [ProducesResponseType(typeof(Response<ChatReplySuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SuggestReply(Guid sessionId, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SuggestChatReplyCommand(sessionId), ct));
}
