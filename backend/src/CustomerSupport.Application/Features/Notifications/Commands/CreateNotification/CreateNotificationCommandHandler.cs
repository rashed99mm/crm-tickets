using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;

public class CreateNotificationCommandHandler(
    IRepository<Notification> notificationRepository,
    IUnitOfWork unitOfWork,
    IMessageFactory messages,
    ILogger<CreateNotificationCommandHandler> logger)
    : ICommandHandler<CreateNotificationCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        logger.LogInformation("Creating notification for user {UserId}", request.UserId);

        var notification = Notification.Create(
            request.UserId,
            request.Title,
            request.Message,
            request.NotificationType,
            request.Channel,
            request.Metadata);

        await notificationRepository.AddAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Notification {NotificationId} created successfully for user {UserId}", notification.Id, request.UserId);

        return messages.Success(notification.Id, ApplicationErrors.Notification.CREATED);
    }
}
