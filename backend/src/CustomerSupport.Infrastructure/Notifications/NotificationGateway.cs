using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Provider-neutral dispatch boundary. Fans out across <see cref="NotificationDispatchRequest.Channels"/>,
/// resolving each <see cref="INotificationChannelSender"/> through <see cref="INotificationDispatcher"/>
/// and aggregating per-channel results. Never calls a provider directly.
/// Persists a delivery record before send and updates it after (NG-6).
/// </summary>
public sealed class NotificationGateway : INotificationGateway
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly INotificationTemplateRenderer _renderer;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NotificationGateway> _logger;

    public NotificationGateway(
        INotificationDispatcher dispatcher,
        INotificationTemplateRenderer renderer,
        AppDbContext dbContext,
        ILogger<NotificationGateway> logger)
    {
        _dispatcher = dispatcher;
        _renderer = renderer;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<NotificationDispatchResult> SendAsync(NotificationDispatchRequest request, CancellationToken ct = default)
    {
        var results = new List<ChannelSendResult>();

        foreach (var channel in request.Channels)
        {
            INotificationChannelSender sender;
            try
            {
                sender = _dispatcher.GetSender(channel);
            }
            catch
            {
                _logger.LogWarning("No channel sender registered for {Channel}", channel.Value);
                results.Add(new ChannelSendResult(channel, false, ApplicationErrors.Notification.CHANNEL_NOT_SUPPORTED));
                continue;
            }

            RenderedNotification rendered;
            try
            {
                rendered = await _renderer.RenderAsync(request, channel, ct);
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Template rendering failed for {Channel}", channel.Value);
                results.Add(new ChannelSendResult(channel, false, ApplicationErrors.Notification.TEMPLATE_INVALID));
                continue;
            }

            var delivery = NotificationDelivery.Create(
                request.RecipientUserId,
                rendered.Email,
                rendered.PhoneNumber,
                channel.Value,
                request.TemplateCode,
                request.CorrelationId);

            await _dbContext.Set<NotificationDelivery>().AddAsync(delivery, ct);
            await _dbContext.SaveChangesAsync(ct);

            try
            {
                var result = await sender.SendAsync(rendered, ct);
                if (result.Succeeded)
                    delivery.RecordSuccess(result.ProviderMessageId);
                else
                    delivery.RecordFailure(result.ErrorCode ?? ApplicationErrors.Notification.DELIVERY_FAILED);
                await _dbContext.SaveChangesAsync(ct);
                results.Add(result);
            }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Channel sender failed for {Channel}", channel.Value);
                delivery.RecordFailure(ApplicationErrors.Notification.DELIVERY_FAILED);
                await _dbContext.SaveChangesAsync(ct);
                results.Add(new ChannelSendResult(channel, false, ApplicationErrors.Notification.DELIVERY_FAILED));
            }
        }

        var succeeded = results.All(r => r.Succeeded);
        return new NotificationDispatchResult(succeeded, results);
    }
}
