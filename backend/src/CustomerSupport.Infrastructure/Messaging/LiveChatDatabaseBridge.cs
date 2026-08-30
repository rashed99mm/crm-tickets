using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Channels;
using CustomerSupport.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

/// <summary>
/// Local development fallback for live chat when RabbitMQ is unavailable. Both API hosts share the
/// same database, so each host can relay newly persisted messages to the SignalR clients connected
/// to that host. RabbitMQ remains the lower-latency transport when it is available.
/// </summary>
public sealed class LiveChatDatabaseBridge(
    IServiceScopeFactory scopeFactory,
    ILogger<LiveChatDatabaseBridge> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);
    private DateTime _lastSeenUtc = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayNewMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Live-chat database realtime bridge failed; retrying");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayNewMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var messages = scope.ServiceProvider.GetRequiredService<IRepository<LiveChatMessage>>();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealTimeNotifier>();
        var deduplicator = scope.ServiceProvider.GetRequiredService<ChatMessagePushedDeduplicator>();

        var rows = await messages.ListOrderedAsync(
            message => message.SentAt > _lastSeenUtc,
            message => message.SentAt,
            descending: false,
            ct);

        foreach (var message in rows)
        {
            _lastSeenUtc = message.SentAt > _lastSeenUtc ? message.SentAt : _lastSeenUtc;

            if (!deduplicator.TryMark(message.Id, DateTimeOffset.UtcNow))
            {
                continue;
            }

            await notifier.NotifyChatMessageAsync(
                new ChatMessagePushPayload(
                    message.Id,
                    message.SessionId,
                    message.SenderType,
                    message.SenderName,
                    message.SenderId,
                    message.Body,
                    message.SentAt),
                ct);
        }
    }
}
