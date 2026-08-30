namespace CustomerSupport.Domain.ValueObjects;

public sealed class NotificationChannel : ValueObject
{
    public string Value { get; }

    public static readonly NotificationChannel InApp = new("InApp");
    public static readonly NotificationChannel Email = new("Email");
    public static readonly NotificationChannel Sms = new("SMS");
    public static readonly NotificationChannel Push = new("Push");
    public static readonly NotificationChannel WhatsApp = new("WhatsApp");

    private NotificationChannel(string value)
    {
        Value = value;
    }

    public static NotificationChannel Create(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel is required", nameof(channel));
        }

        return channel.Trim() switch
        {
            "InApp" => InApp,
            "Email" => Email,
            "SMS" => Sms,
            "Push" => Push,
            "WhatsApp" => WhatsApp,
            _ => throw new ArgumentException($"Invalid notification channel: {channel}. Must be InApp, Email, SMS, Push, or WhatsApp.", nameof(channel))
        };
    }

    public static bool TryCreate(string? channel, out NotificationChannel? result, out string? error)
    {
        try
        {
            result = Create(channel);
            error = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    public bool IsInApp => this == InApp;
    public bool IsEmail => this == Email;
    public bool IsSms => this == Sms;
    public bool IsPush => this == Push;
    public bool IsWhatsApp => this == WhatsApp;

    public static implicit operator string(NotificationChannel channel) => channel.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
