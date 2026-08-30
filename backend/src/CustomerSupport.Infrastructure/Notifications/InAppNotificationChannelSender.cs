using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Services;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// In-app channel: persists a durable <see cref="Notification"/> row (Channel=InApp) and pushes the
/// rendered payload over SignalR to the recipient's user group. The row is persisted before the
/// SignalR push, and a failed push marks the row failed rather than pretending success.
/// </summary>
public sealed class InAppNotificationChannelSender : INotificationChannelSender
{
    private readonly INotificationDomainService _domainService;
    private readonly AppDbContext _dbContext;
    private readonly IRealTimeNotifier _notifier;
    private readonly ILogger<InAppNotificationChannelSender> _logger;

    public InAppNotificationChannelSender(
        INotificationDomainService domainService,
        AppDbContext dbContext,
        IRealTimeNotifier notifier,
        ILogger<InAppNotificationChannelSender> logger)
    {
        _domainService = domainService;
        _dbContext = dbContext;
        _notifier = notifier;
        _logger = logger;
    }

    public NotificationChannel SupportedChannel => NotificationChannel.InApp;

    public async Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default)
    {
        if (notification.RecipientUserId is not { } userId)
            return new ChannelSendResult(NotificationChannel.InApp, false, ApplicationErrors.Notification.INAPP_REQUIRES_USER);

        var entity = _domainService.CreateNotification(
            userId,
            notification.Title,
            notification.Message,
            notification.NotificationType,
            NotificationChannel.InApp.Value);

        await _dbContext.Set<Notification>().AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            await _notifier.NotifyInAppAsync(userId, new InAppPushPayload(
                entity.Id,
                notification.Title,
                notification.Message,
                notification.NotificationType,
                entity.CreatedAt), ct);

            entity.Send();
            await _dbContext.SaveChangesAsync(ct);
            return new ChannelSendResult(NotificationChannel.InApp, true, ProviderMessageId: entity.Id.ToString());
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "SignalR push failed for in-app notification {NotificationId}", entity.Id);
            entity.MarkAsFailed(ApplicationErrors.Notification.SIGNAL_FAILED);
            await _dbContext.SaveChangesAsync(ct);
            return new ChannelSendResult(NotificationChannel.InApp, false, ApplicationErrors.Notification.SIGNAL_FAILED);
        }
    }
}
