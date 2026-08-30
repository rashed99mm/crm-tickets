using CustomerSupport.Domain.Entities.Notifications;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Jobs;

public class NotificationSender : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationSender> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public NotificationSender(IServiceProvider serviceProvider, ILogger<NotificationSender> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Sender started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notifications");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessPendingNotificationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await dbContext.Set<Notification>()
            .IgnoreQueryFilters()
            .Where(n => n.Status == "Pending" && !n.IsDeleted)
            .OrderBy(n => n.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (var notification in pending)
        {
            try
            {
                await SendNotificationAsync(notification, ct);
                notification.Send();
                _logger.LogInformation("Sent notification {NotificationId} to User {UserId}", 
                    notification.Id, notification.UserId);
            }
            catch (Exception ex)
            {
                notification.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Failed to send notification {NotificationId}", notification.Id);
            }
        }

        if (pending.Any())
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private Task SendNotificationAsync(Notification notification, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
