namespace CustomerSupport.Application.Features.Admin.Dtos;

/// <summary>One audit trail entry — AC-140.</summary>
public record AuditLogDto(
    Guid Id, Guid UserId, string? UserName, string Action, string EntityType, Guid EntityId,
    string? OldValues, string? NewValues, string? IpAddress, string? UserAgent, DateTime CreatedAt);
