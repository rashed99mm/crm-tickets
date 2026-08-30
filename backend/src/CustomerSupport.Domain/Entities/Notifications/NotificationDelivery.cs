using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Notifications;

/// <summary>
/// Append-only record of an outbound notification dispatch attempt through the gateway (FEAT-15, NG-6).
/// Persisted before send; updated with the provider response after. Duplicate detection is on
/// <c>(Channel, ProviderMessageId)</c> for channels that return a provider-assigned message ID.
/// </summary>
public class NotificationDelivery : BaseEntity
{
    public const int MaxProviderMessageIdLength = 200;

    public Guid? RecipientUserId { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string TemplateCode { get; private set; } = string.Empty;
    public string Status { get; private set; } = DeliveryStatus.Pending;
    public string? ProviderMessageId { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? CorrelationId { get; private set; }

    public static NotificationDelivery Create(
        Guid? recipientUserId,
        string? email,
        string? phoneNumber,
        string channel,
        string templateCode,
        string? correlationId,
        string? providerMessageId = null)
    {
        return new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Email = email,
            PhoneNumber = phoneNumber,
            Channel = channel,
            TemplateCode = templateCode,
            CorrelationId = correlationId,
            ProviderMessageId = providerMessageId,
            Status = DeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordSuccess(string? providerMessageId)
    {
        Status = DeliveryStatus.Delivered;
        ProviderMessageId = providerMessageId ?? ProviderMessageId;
        AttemptCount++;
        MarkUpdated();
    }

    public void RecordFailure(string errorCode)
    {
        Status = DeliveryStatus.Failed;
        ErrorCode = errorCode;
        AttemptCount++;
        MarkUpdated();
    }

    public static class DeliveryStatus
    {
        public const string Pending = "Pending";
        public const string Delivered = "Delivered";
        public const string Failed = "Failed";
    }
}
