using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Ai;
using CustomerSupport.Application.Features.Contents.Dtos;
using CustomerSupport.Application.Features.Contents.Queries.GetFaqContents;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>Authenticated knowledge-base assistant for the internal shell.</summary>
[ApiController]
[Route("api/knowledge-base")]
[Produces("application/json")]
[Authorize]
public sealed class KnowledgeBaseAiController(IMediator mediator) : ControllerBase
{
    [HttpGet("articles/faq")]
    [ProducesResponseType(typeof(Response<PaginatedList<ContentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaqArticles(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 3,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFaqContentsQuery(searchTerm, skip, take), ct);
        return this.ToActionResult(result);
    }

    [HttpPost("ask")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<AiAnswerDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask([FromBody] AskKnowledgeBaseRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AskKnowledgeBaseCommand(request.Question), ct);
        return this.ToActionResult(result);
    }
}

public sealed record AskKnowledgeBaseRequest(string Question);
