using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Domain.Entities.Audit;
using CustomerSupport.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Behaviors;

public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUserContext _userContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    private static readonly HashSet<string> AuditableCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateUserCommand", "UpdateUserCommand", "DeleteUserCommand",
        "CreateContentCommand", "UpdateContentCommand", "DeleteContentCommand",
        "CreateNotificationCommand", "DeleteNotificationCommand",
        "CreatePlatformSettingCommand", "UpdatePlatformSettingCommand", "DeletePlatformSettingCommand"
    };

    private static readonly Dictionary<string, string> EntityTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CreateUserCommand", "User" },
        { "UpdateUserCommand", "User" },
        { "DeleteUserCommand", "User" },
        { "CreateContentCommand", "Content" },
        { "UpdateContentCommand", "Content" },
        { "DeleteContentCommand", "Content" },
        { "CreateNotificationCommand", "Notification" },
        { "DeleteNotificationCommand", "Notification" },
        { "CreatePlatformSettingCommand", "PlatformSetting" },
        { "UpdatePlatformSettingCommand", "PlatformSetting" },
        { "DeletePlatformSettingCommand", "PlatformSetting" }
    };

    public AuditBehavior(
        IUserContext userContext, IAuditService auditService, ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _userContext = userContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        if (!AuditableCommands.Contains(requestName))
        {
            return await next();
        }

        var userId = _userContext.UserId;
        _logger.LogDebug("Audit: Starting {RequestName} by User {UserId}", requestName, userId);

        var response = await next();

        _logger.LogDebug("Audit: Completed {RequestName}", requestName);

        // US-801/802's audit log has no consumer without this — until now, IAuditService existed
        // and was registered in DI but nothing ever called it, so AuditLogs was permanently empty.
        await RecordAsync(requestName, request, response, userId, cancellationToken);

        return response;
    }

    /// <summary>
    /// Best-effort, generic across every auditable command: there is no "before" snapshot (this
    /// behavior only sees the request and the response, not a pre-handler read), so
    /// <see cref="AuditLog.OldValues"/> is always null here. A failed operation changed nothing and
    /// is not logged — <c>Response&lt;T&gt;.Success</c> is read via reflection since this behavior
    /// is generic over <typeparamref name="TResponse"/> and has no common interface to check
    /// against.
    /// </summary>
    private async Task RecordAsync(
        string requestName, TRequest request, TResponse response, Guid userId, CancellationToken ct)
    {
        if (typeof(TResponse).GetProperty("Success")?.GetValue(response) is not true)
        {
            return;
        }

        var entityId = ResolveEntityId(request, response);
        if (entityId is null)
        {
            // Nothing to key the entry to — skip rather than log a Guid.Empty that would read as
            // a real entity id.
            return;
        }

        var entityType = EntityTypeMapping.GetValueOrDefault(requestName, "Unknown");
        var action = requestName.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ? "Created"
            : requestName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? "Deleted"
            : "Updated";

        var auditLog = AuditLog.Create(
            userId,
            _userContext.Email,
            action,
            entityType,
            entityId.Value,
            oldValues: null,
            newValues: action == "Deleted" ? null : request);

        await _auditService.LogAsync(auditLog, ct);
    }

    /// <summary>
    /// A create command's new id is the response's <c>Data</c>; an update/delete command's target
    /// id is a property named <c>Id</c> on the request itself. Both are read by reflection because
    /// this behavior has to work across every auditable command's distinct shape.
    /// </summary>
    private static Guid? ResolveEntityId(TRequest request, TResponse response)
    {
        if (typeof(TResponse).GetProperty("Data")?.GetValue(response) is Guid fromResponse)
        {
            return fromResponse;
        }

        if (typeof(TRequest).GetProperty("Id")?.GetValue(request) is Guid fromRequest)
        {
            return fromRequest;
        }

        return null;
    }
}
