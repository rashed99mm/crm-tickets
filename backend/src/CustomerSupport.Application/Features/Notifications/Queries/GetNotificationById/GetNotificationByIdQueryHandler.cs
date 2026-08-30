using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Notifications.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Notifications.Queries.GetNotificationById;

public class GetNotificationByIdQueryHandler(
    IRepository<Notification> notificationRepository,
    IMessageFactory messages,
    ILogger<GetNotificationByIdQueryHandler> logger)
    : IQueryHandler<GetNotificationByIdQuery, Response<NotificationDto>>
{
    public async Task<Response<NotificationDto>> Handle(GetNotificationByIdQuery request, CancellationToken ct)
    {
        logger.LogInformation("Retrieving notification {NotificationId} for user {UserId}", request.Id, request.RequestedByUserId);

        var notification = await notificationRepository.GetByIdAsync(request.Id, ct);
        if (notification == null)
        {
            logger.LogWarning("Notification {NotificationId} not found", request.Id);
            return messages.NotFound<NotificationDto>(ApplicationErrors.Notification.NOT_FOUND);
        }

        if (notification.UserId != request.RequestedByUserId)
        {
            logger.LogWarning("Access denied — user {UserId} attempted to access notification {NotificationId} owned by {OwnerId}", request.RequestedByUserId, request.Id, notification.UserId);
            return messages.Fail<NotificationDto>(ApplicationErrors.Notification.ACCESS_DENIED, MessageType.Forbidden);
        }

        return messages.Success(MapToDto(notification), ApplicationErrors.General.SUCCESS_OPERATION);
    }

    private static NotificationDto MapToDto(Notification notification) => new(
        notification.Id,
        notification.UserId,
        notification.Title,
        notification.Message,
        notification.NotificationType,
        notification.Channel,
        notification.Status,
        notification.ReadAt,
        notification.SentAt,
        notification.RetryCount,
        notification.CreatedAt
    );
}
