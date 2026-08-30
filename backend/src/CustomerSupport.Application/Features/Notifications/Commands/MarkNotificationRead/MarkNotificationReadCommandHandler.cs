using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandHandler(
    IRepository<Notification> notificationRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<MarkNotificationReadCommandHandler> logger)
    : ICommandHandler<MarkNotificationReadCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        logger.LogInformation("Marking notification {NotificationId} as read", request.Id);

        var notification = await notificationRepository.GetByIdAsync(request.Id, ct);
        if (notification == null)
        {
            logger.LogWarning("Mark read failed — notification {NotificationId} not found", request.Id);
            return messages.NotFound<Unit>(ApplicationErrors.Notification.NOT_FOUND);
        }

        notification.MarkAsRead();
        notificationRepository.Update(notification);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Notification {NotificationId} marked as read", request.Id);

        return messages.Success(Unit.Value, ApplicationErrors.Notification.MARKED_READ);
    }
}
