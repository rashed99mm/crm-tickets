using Asp.Versioning;
using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Notifications.Commands.MarkNotificationRead;
using CustomerSupport.Application.Features.Notifications.Dtos;
using CustomerSupport.Application.Features.Notifications.Queries.GetNotificationById;
using CustomerSupport.Application.Features.Notifications.Queries.GetNotifications;
using CustomerSupport.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupport.ExternalApi.Controllers;

/// <summary>Customer-owned notification inbox for the external portal.</summary>
[ApiController]
[Route("api/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(Policy = "Authenticated")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetNotificationsQuery(User.GetRequiredUserId())
            {
                PageIndex = page,
                PageSize = pageSize,
            }, ct);

        return this.ToActionResult(result);
    }

    [HttpGet("unread/count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await mediator.Send(
            new CustomerSupport.Application.Features.Notifications.Queries.GetUnreadNotificationCount.GetUnreadNotificationCountQuery(
                User.GetRequiredUserId()), ct);

        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var owned = await mediator.Send(new GetNotificationByIdQuery(id, userId), ct);
        if (!owned.Success)
        {
            return this.ToActionResult(owned);
        }

        var result = await mediator.Send(new MarkNotificationReadCommand(id), ct);
        return this.ToActionResult(result, StatusCodes.Status204NoContent);
    }
}
