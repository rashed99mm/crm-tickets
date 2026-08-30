namespace CustomerSupport.Application.Features.Notifications.Commands.CreateNotification;

public record CreateNotificationRequest(
    Guid UserId,
    string Title,
    string Message,
    string NotificationType,
    string Channel,
    string? Metadata
);
