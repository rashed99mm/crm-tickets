namespace CustomerSupport.Application.Notifications;

/// <summary>
/// Shared constants for the notification gateway. Kept here (Application) so both Infrastructure
/// (channel senders) and Api.Shared (real-time notifier) reference the same values without a
/// magic-string dependency.
/// </summary>
public static class NotificationGatewayConstants
{
    /// <summary>External API configuration name for the email integration.</summary>
    public const string EmailGatewayConfigName = "EmailGateway";

    /// <summary>External API configuration name for the SMS integration.</summary>
    public const string SmsGatewayConfigName = "SmsGateway";

    /// <summary>External API configuration name for the WhatsApp integration.</summary>
    public const string WhatsAppGatewayConfigName = "WhatsAppGateway";

    /// <summary>SignalR group prefix for per-user in-app delivery: <c>user:{userId}</c>.</summary>
    public const string SignalRUserGroupPrefix = "user:";

    /// <summary>SignalR group prefix for a live-chat session: <c>chat:{sessionId}</c>.</summary>
    public const string SignalRChatSessionGroupPrefix = "chat:";

    /// <summary>SignalR client method invoked when an in-app notification arrives.</summary>
    public const string SignalRInAppMethod = "NotificationReceived";

    /// <summary>SignalR client method invoked when a live-chat message arrives.</summary>
    public const string SignalRChatMessageMethod = "ChatMessageReceived";

    /// <summary>Bounded retry count for transient provider failures (timeout / 5xx).</summary>
    public const int TransientRetryCount = 3;

    public static string UserGroup(Guid userId) => $"{SignalRUserGroupPrefix}{userId}";

    public static string ChatSessionGroup(Guid sessionId) => $"{SignalRChatSessionGroupPrefix}{sessionId}";
}
