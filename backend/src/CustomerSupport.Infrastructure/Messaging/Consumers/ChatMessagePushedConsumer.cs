using CustomerSupport.Application.Notifications;
using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

/// <summary>
/// The single source of the live-chat real-time push. Runs on both hosts (via the shared
/// <c>ConfigureMessaging</c>); whichever host owns a live connection for the session is the one whose
/// <see cref="IRealTimeNotifier"/> instance reaches it. This is what delivers a message across the
/// InternalApi/ExternalApi process boundary: an agent message published on the internal host is
/// carried by the bus to the external host, whose consumer pushes it into that host's anonymous
/// <c>/hubs/chat</c> session group (CC-30/CC-31/CC-34). Duplicate publishes are dropped by the
/// in-process <see cref="ChatMessagePushedDeduplicator"/> (CC-32); with no bus
/// (<c>NoOpMessagePublisher</c>) nothing is published and nothing is pushed, leaving the transcript
/// fallback (CC-33).
/// </summary>
public sealed class ChatMessagePushedConsumer : IConsumer<ChatMessagePushed>
{
    private readonly IRealTimeNotifier _realtime;
    private readonly ChatMessagePushedDeduplicator _deduplicator;
    private readonly ILogger<ChatMessagePushedConsumer> _logger;

    public ChatMessagePushedConsumer(
        IRealTimeNotifier realtime,
        ChatMessagePushedDeduplicator deduplicator,
        ILogger<ChatMessagePushedConsumer> logger)
    {
        _realtime = realtime;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ChatMessagePushed> context)
    {
        var message = context.Message;

        if (!_deduplicator.TryMark(message.MessageId, DateTimeOffset.UtcNow))
        {
            _logger.LogInformation("Dropping duplicate ChatMessagePushed for message {MessageId}", message.MessageId);
            return;
        }

        _logger.LogDebug(
            "Pushing chat message {MessageId} for session {SessionId} from {SenderType}",
            message.MessageId,
            message.SessionId,
            message.SenderType);

        await _realtime.NotifyChatMessageAsync(new ChatMessagePushPayload(
            message.MessageId,
            message.SessionId,
            message.SenderType,
            message.SenderName,
            message.SenderId,
            message.Body,
            message.SentAt), context.CancellationToken);
    }
}
