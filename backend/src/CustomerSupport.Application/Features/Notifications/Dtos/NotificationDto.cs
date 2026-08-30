namespace CustomerSupport.Application.Features.Notifications.Dtos;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string NotificationType,
    string Channel,
    string Status,
    DateTime? ReadAt,
    DateTime? SentAt,
    int RetryCount,
    DateTime CreatedAt
);
