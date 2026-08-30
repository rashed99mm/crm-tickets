namespace CustomerSupport.Domain.Events.Notifications;

public sealed class NotificationSentEvent : DomainEvent
{
    public Guid NotificationId { get; }
    public Guid UserId { get; }
    public string Channel { get; }

    public NotificationSentEvent(Guid notificationId, Guid userId, string channel)
    {
        NotificationId = notificationId;
        UserId = userId;
        Channel = channel;
    }
}
