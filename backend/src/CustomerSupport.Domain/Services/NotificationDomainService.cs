using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Services;

public interface INotificationDomainService
{
    Notification CreateNotification(
        Guid userId,
        string title,
        string message,
        string notificationType,
        string channel,
        string? metadata = null);
}

public class NotificationDomainService : INotificationDomainService
{
    public Notification CreateNotification(
        Guid userId,
        string title,
        string message,
        string notificationType,
        string channel,
        string? metadata = null)
    {
        return Notification.Create(userId, title, message, notificationType, channel, metadata);
    }
}
