using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Notifications.Commands.DeleteNotification;

public class DeleteNotificationCommandHandler(
    IRepository<Notification> notificationRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<DeleteNotificationCommandHandler> logger)
    : ICommandHandler<DeleteNotificationCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeleteNotificationCommand request, CancellationToken ct)
    {
        logger.LogInformation("Deleting notification {NotificationId}", request.Id);

        var notification = await notificationRepository.GetByIdAsync(request.Id, ct);
        if (notification == null)
        {
            logger.LogWarning("Delete failed — notification {NotificationId} not found", request.Id);
            return messages.NotFound<Unit>(ApplicationErrors.Notification.NOT_FOUND);
        }

        notification.SoftDelete();
        notificationRepository.Update(notification);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Notification {NotificationId} deleted successfully", request.Id);

        return messages.Success(Unit.Value, ApplicationErrors.General.SUCCESS_DELETED);
    }
}
