using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Jobs;

/// <summary>
/// Loops <see cref="ISlaBreachScanner"/> on a fixed interval — the same shape
/// <see cref="NotificationSender"/> already uses, rather than introducing Hangfire's recurring-job
/// API as this feature's first real use of it (spec A6).
/// </summary>
public class SlaBreachDetector : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaBreachDetector> _logger;
    private readonly TimeSpan _interval;

    public SlaBreachDetector(
        IServiceProvider serviceProvider,
        ILogger<SlaBreachDetector> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var minutes = configuration.GetValue("SlaAutomation:ScanIntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA breach detector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<ISlaBreachScanner>();
                var recorded = await scanner.ScanAsync(stoppingToken);

                if (recorded > 0)
                {
                    _logger.LogInformation("Recorded {Count} SLA breach event(s)", recorded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for SLA breaches");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
