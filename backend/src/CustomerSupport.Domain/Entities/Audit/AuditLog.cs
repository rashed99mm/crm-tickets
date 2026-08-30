namespace CustomerSupport.Domain.Entities.Audit;

public class AuditLog : BaseEntity
{
    public Guid UserId { get; private set; }
    public string? UserName { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static AuditLog Create(
        Guid userId,
        string? userName,
        string action,
        string entityType,
        Guid entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AuditLog LogCreate(
        Guid userId,
        string? userName,
        string entityType,
        Guid entityId,
        object newEntity,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return Create(userId, userName, "Created", entityType, entityId, null, newEntity, ipAddress, userAgent);
    }

    public static AuditLog LogUpdate(
        Guid userId,
        string? userName,
        string entityType,
        Guid entityId,
        object oldEntity,
        object newEntity,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return Create(userId, userName, "Updated", entityType, entityId, oldEntity, newEntity, ipAddress, userAgent);
    }

    public static AuditLog LogDelete(
        Guid userId,
        string? userName,
        string entityType,
        Guid entityId,
        object deletedEntity,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return Create(userId, userName, "Deleted", entityType, entityId, deletedEntity, null, ipAddress, userAgent);
    }
}
