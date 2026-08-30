namespace CustomerSupport.Domain.Entities.Support;

public class QuickReply : BaseEntity
{
    public string Shortcut { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    public static QuickReply Create(string shortcut, string body)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            throw new ArgumentException("Shortcut is required", nameof(shortcut));

        if (shortcut.Length > 20)
            throw new ArgumentException("Shortcut must not exceed 20 characters", nameof(shortcut));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required", nameof(body));

        if (body.Length > 1000)
            throw new ArgumentException("Body must not exceed 1000 characters", nameof(body));

        return new QuickReply
        {
            Id = Guid.NewGuid(),
            Shortcut = shortcut.Trim().ToUpperInvariant(),
            Body = body.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
