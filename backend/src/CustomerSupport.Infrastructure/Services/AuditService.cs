using CustomerSupport.Domain.Entities.Audit;
using CustomerSupport.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CustomerSupport.Infrastructure.Persistence;

namespace CustomerSupport.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext dbContext, ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        try
        {
            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Audit log created: {Action} {EntityType} {EntityId}", 
                auditLog.Action, auditLog.EntityType, auditLog.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for {EntityType} {EntityId}", 
                auditLog.EntityType, auditLog.EntityId);
        }
    }

    public async Task CleanupOldLogsAsync(int retentionDays, CancellationToken ct = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var oldLogs = await _dbContext.AuditLogs
                .Where(x => x.CreatedAt < cutoffDate)
                .ToListAsync(ct);

            if (oldLogs.Any())
            {
                _dbContext.AuditLogs.RemoveRange(oldLogs);
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Cleaned up {Count} old audit logs", oldLogs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old audit logs");
        }
    }
}
