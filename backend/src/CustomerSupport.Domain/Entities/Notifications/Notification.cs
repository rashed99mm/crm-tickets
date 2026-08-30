using CustomerSupport.Domain.Entities;
using CustomerSupport.Domain.Events.Notifications;
using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Domain.Entities.Notifications;

public class Notification : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string NotificationType { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "InApp";
    public string Status { get; private set; } = "Pending";
    public DateTime? ReadAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? Metadata { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static Notification Create(
        Guid userId,
        string title,
        string message,
        string notificationType,
        string channel,
        string? metadata = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));
        if (string.IsNullOrWhiteSpace(notificationType))
            throw new ArgumentException("NotificationType is required", nameof(notificationType));
        if (string.IsNullOrWhiteSpace(channel))
            channel = "InApp";

        var channelVo = NotificationChannel.Create(channel);

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            Message = message,
            NotificationType = notificationType,
            Channel = channelVo.Value,
            Metadata = metadata,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Send()
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot send notification with status '{Status}'.");

        Status = "Sent";
        SentAt = DateTime.UtcNow;
        MarkUpdated();

        AddDomainEvent(new NotificationSentEvent(Id, UserId, Channel));
    }

    public void MarkAsRead()
    {
        if (ReadAt.HasValue)
            return;

        ReadAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        RetryCount++;
        MarkUpdated();
    }

    public bool CanRetry(int maxRetries = 3)
    {
        return Status == "Failed" && RetryCount < maxRetries;
    }

    public void ResetForRetry()
    {
        if (!CanRetry())
            throw new InvalidOperationException("Notification has exceeded maximum retry attempts.");

        Status = "Pending";
        ErrorMessage = null;
        MarkUpdated();
    }

    public void Cancel()
    {
        if (Status == "Sent")
            throw new InvalidOperationException("Cannot cancel a notification that has already been sent.");

        Status = "Cancelled";
        MarkUpdated();
    }

    public bool IsPending => Status == "Pending";
    public bool IsSent => Status == "Sent";
    public bool IsFailed => Status == "Failed";
    public bool IsCancelled => Status == "Cancelled";
    public bool IsRead => ReadAt.HasValue;
}
