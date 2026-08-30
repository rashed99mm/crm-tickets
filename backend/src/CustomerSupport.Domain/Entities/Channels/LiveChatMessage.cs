using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities;

namespace CustomerSupport.Domain.Entities.Channels;

public class LiveChatMessage : BaseEntity, IAppendOnlyEntity
{
    private static readonly string[] AllowedSenders = ["Customer", "Agent", "System"];

    public Guid SessionId { get; private set; }
    public string SenderType { get; private set; } = string.Empty;
    public string SenderName { get; private set; } = string.Empty;
    public Guid? SenderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    private LiveChatMessage() { }

    public static LiveChatMessage Create(
        Guid sessionId, string senderType, string senderName, Guid? senderId, string body)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A live chat session is required", nameof(sessionId));
        }

        if (!AllowedSenders.Contains(senderType))
        {
            throw new ArgumentException($"Sender type must be one of: {string.Join(", ", AllowedSenders)}", nameof(senderType));
        }

        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("Sender name is required", nameof(senderName));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Message body is required", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Message body must not exceed 4000 characters", nameof(body));
        }

        var now = DateTime.UtcNow;
        return new LiveChatMessage
        {
            SessionId = sessionId,
            SenderType = senderType,
            SenderName = senderName.Trim(),
            SenderId = senderId,
            Body = body.Trim(),
            SentAt = now,
            CreatedAt = now,
            CreatedBy = senderId,
        };
    }
}
