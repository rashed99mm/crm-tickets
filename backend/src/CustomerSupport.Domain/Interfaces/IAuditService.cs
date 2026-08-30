using CustomerSupport.Domain.Entities.Audit;

namespace CustomerSupport.Domain.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditLog auditLog, CancellationToken ct = default);
    Task CleanupOldLogsAsync(int retentionDays, CancellationToken ct = default);
}
