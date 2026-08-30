using CustomerSupport.Domain.ValueObjects;

namespace CustomerSupport.Application.Notifications;

public sealed record NotificationDispatchRequest(
    string TemplateCode,
    Guid? RecipientUserId,
    IReadOnlyCollection<NotificationChannel> Channels,
    IReadOnlyDictionary<string, string> Variables,
    string? Email,
    string? PhoneNumber,
    bool BypassUserSettings,
    string? DeduplicationKey,
    string? CorrelationId);

public sealed record RenderedNotification(
    Guid? RecipientUserId,
    string? Email,
    string? PhoneNumber,
    string Title,
    string Message,
    string NotificationType,
    NotificationChannel Channel,
    string? Locale);

public sealed record ChannelSendResult(NotificationChannel Channel, bool Succeeded, string? ErrorCode = null, string? ProviderMessageId = null);

public sealed record NotificationDispatchResult(
    bool Succeeded,
    IReadOnlyCollection<ChannelSendResult> ChannelResults);

public sealed record InAppPushPayload(Guid Id, string Title, string Message, string Type, DateTime CreatedAt);
public sealed record ChatMessagePushPayload(
    Guid Id,
    Guid SessionId,
    string SenderType,
    string SenderName,
    Guid? SenderId,
    string Body,
    DateTime SentAt);

public interface INotificationChannelSender
{
    NotificationChannel SupportedChannel { get; }
    Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default);
}

public interface INotificationGateway
{
    Task<NotificationDispatchResult> SendAsync(NotificationDispatchRequest request, CancellationToken ct = default);
}

public interface INotificationDispatcher
{
    IReadOnlyCollection<INotificationChannelSender> Senders { get; }
    INotificationChannelSender GetSender(NotificationChannel channel);
}

public interface INotificationTemplateRenderer
{
    Task<RenderedNotification> RenderAsync(NotificationDispatchRequest request, NotificationChannel channel, CancellationToken ct = default);
}

/// <summary>
/// Live transport for the in-app channel. Implemented in Api.Shared (which owns SignalR) so
/// Infrastructure never references the web host assembly.
/// </summary>
public interface IRealTimeNotifier
{
    Task NotifyInAppAsync(Guid userId, InAppPushPayload payload, CancellationToken ct = default);
    Task NotifyChatMessageAsync(ChatMessagePushPayload payload, CancellationToken ct = default);
}
