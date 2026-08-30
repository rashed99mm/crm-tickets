using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Chat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupport.ExternalApi.Controllers;

[ApiController]
[Route("api/external/chat")]
[ApiVersion("1.0")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("fixed")]
public class ChatController(IMediator mediator) : ControllerBase
{
    [HttpPost("start")]
    [ProducesResponseType(typeof(Response<StartChatSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start([FromBody] StartChatSessionRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(
            new StartAnonymousChatSessionCommand(request.CustomerName, request.CustomerEmail, request.InitialMessage),
            ct));

    [HttpPost("messages")]
    [ProducesResponseType(typeof(Response<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send([FromBody] AnonymousChatMessageRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new SendAnonymousChatMessageCommand(request.Token, request.Body), ct));

    [HttpGet("messages")]
    [ProducesResponseType(typeof(Response<IReadOnlyList<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transcript([FromQuery] string token, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new GetAnonymousChatTranscriptQuery(token), ct));
}

public sealed record AnonymousChatMessageRequest(string Token, string Body);
