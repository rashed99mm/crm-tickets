using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain;
using CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;
using CustomerSupport.Application.Features.Notifications.Commands.DeleteNotification;
using CustomerSupport.Application.Features.Notifications.Commands.MarkNotificationRead;
using NotificationDto = CustomerSupport.Application.Features.Notifications.Dtos.NotificationDto;
using CustomerSupport.Application.Features.Notifications.Queries.GetNotificationById;
using CustomerSupport.Application.Features.Notifications.Queries.GetNotifications;
using CustomerSupport.Application.Features.Notifications.Queries.GetUnreadNotificationCount;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Features.Notifications.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;

namespace CustomerSupport.InternalApi.Controllers;

/// <summary>
/// Manages user notifications and unread counts.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(IMediator mediator, ILogger<NotificationsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves paginated notifications for the current user.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(Response<PaginatedList<NotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] string? status = null,
        [FromQuery] string? notificationType = null,
        [FromQuery] bool? isRead = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Notifications list requested by user {UserId}", User.GetRequiredUserId());

        var query = new GetNotificationsQuery(User.GetRequiredUserId())
        {
            PageIndex = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Status = status,
            NotificationType = notificationType,
            IsRead = isRead
        };
        
        var result = await _mediator.Send(query, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves the unread notification count for the current user.
    /// </summary>
    [HttpGet("unread/count")]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        _logger.LogInformation("Unread notification count requested by user {UserId}", User.GetRequiredUserId());

        var result = await _mediator.Send(new GetUnreadNotificationCountQuery(User.GetRequiredUserId()), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Retrieves a specific notification by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(Response<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Notification {NotificationId} requested by user {UserId}", id, User.GetRequiredUserId());

        var result = await _mediator.Send(
            new GetNotificationByIdQuery(id, User.GetRequiredUserId()), ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Creates a new notification for a user.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(Response<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Notification creation requested for user {UserId}", request.UserId);

        var command = new CreateNotificationCommand(
            request.UserId,
            request.Title,
            request.Message,
            request.NotificationType,
            request.Channel ?? "InApp",
            request.Metadata
        );

        var result = await _mediator.Send(command, ct);
        return this.ToActionResult(result, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Mark as read requested for notification {NotificationId} by user {UserId}", id, User.GetRequiredUserId());

        var checkResult = await _mediator.Send(
            new GetNotificationByIdQuery(id, User.GetRequiredUserId()), ct);
        
        if (!checkResult.Success)
        {
            return this.ToActionResult(checkResult);
        }

        var result = await _mediator.Send(new MarkNotificationReadCommand(id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Soft-deletes a notification.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Response<Unit>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Delete requested for notification {NotificationId} by user {UserId}", id, User.GetRequiredUserId());

        var checkResult = await _mediator.Send(
            new GetNotificationByIdQuery(id, User.GetRequiredUserId()), ct);
        
        if (!checkResult.Success)
        {
            return this.ToActionResult(checkResult);
        }

        var result = await _mediator.Send(new DeleteNotificationCommand(id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }
}
