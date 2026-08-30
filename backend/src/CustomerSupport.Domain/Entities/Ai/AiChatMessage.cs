using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Entities.Ai;

/// <summary>Who authored a turn.</summary>
public enum AiChatRole
{
    User = 0,
    Assistant = 1
}

/// <summary>
/// One turn of a conversation. The assistant's citations are stored as JSON exactly as they were
/// returned, never re-derived, so what the user saw is what the record shows.
/// </summary>
public class AiChatMessage : BaseEntity
{
    public Guid SessionId { get; private set; }
    public AiChatRole Role { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string CitationsJson { get; private set; } = "[]";
    public DateTime CreatedAtUtc { get; private set; }

    private AiChatMessage() { }

    public static AiChatMessage UserTurn(Guid sessionId, string body)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A session is required", nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A message body is required", nameof(body));
        }

        return new AiChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = AiChatRole.User,
            Body = body.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static AiChatMessage AssistantTurn(Guid sessionId, string body, string citationsJson)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A session is required", nameof(sessionId));
        }

        return new AiChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = AiChatRole.Assistant,
            Body = body,
            CitationsJson = string.IsNullOrWhiteSpace(citationsJson) ? "[]" : citationsJson,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
