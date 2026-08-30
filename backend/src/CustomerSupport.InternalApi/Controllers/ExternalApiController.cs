using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.ExternalApis.Queries.GetComments;
using CustomerSupport.Application.ExternalApis.Queries.GetPostById;
using CustomerSupport.Application.ExternalApis.Queries.GetPosts;
using CustomerSupport.Application.ExternalApis.Queries.GetWeather;
using CustomerSupport.Api.Shared.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Proxies requests to external APIs for demonstration and integration testing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExternalApiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExternalApiController> _logger;

    public ExternalApiController(IMediator mediator, ILogger<ExternalApiController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves posts from an external placeholder API.
    /// </summary>
    [HttpGet("posts")]
    [ProducesResponseType(typeof(Response<List<PostDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosts(CancellationToken cancellationToken)
    {
        _logger.LogInformation("External posts requested");
        return this.ToActionResult(await _mediator.Send(new GetPostsQuery(), cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific post from an external placeholder API.
    /// </summary>
    [HttpGet("posts/{id}")]
    [ProducesResponseType(typeof(Response<PostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPost(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("External post {PostId} requested", id);
        return this.ToActionResult(await _mediator.Send(new GetPostByIdQuery(id), cancellationToken));
    }

    /// <summary>
    /// Retrieves comments for a post from an external placeholder API.
    /// </summary>
    [HttpGet("comments/{postId}")]
    [ProducesResponseType(typeof(Response<List<CommentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(int postId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("External comments for post {PostId} requested", postId);
        return this.ToActionResult(await _mediator.Send(new GetCommentsQuery(postId), cancellationToken));
    }

    /// <summary>
    /// Retrieves weather data for a city from an external weather API.
    /// </summary>
    [HttpGet("weather")]
    [ProducesResponseType(typeof(Response<WeatherDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeather(
        [FromQuery] string city = "London",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Weather requested for city {City}", city);
        return this.ToActionResult(await _mediator.Send(new GetWeatherQuery(city), cancellationToken));
    }
}
