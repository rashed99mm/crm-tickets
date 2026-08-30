using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Admin.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Audit;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Admin.Queries.GetAuditLog;

public class GetAuditLogQueryHandler(
    IRepository<AuditLog> auditLogs,
    IMessageFactory messages)
    : IQueryHandler<GetAuditLogQuery, Response<PaginatedList<AuditLogDto>>>
{
    public async Task<Response<PaginatedList<AuditLogDto>>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<AuditLog>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.ActionType), a => a.Action == request.ActionType!)
            .WhereIf(request.UserId.HasValue, a => a.UserId == request.UserId!.Value);

        var page = await auditLogs.GetPagedAsync(
            request,
            filter,
            a => new AuditLogDto(
                a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId,
                a.OldValues, a.NewValues, a.IpAddress, a.UserAgent, a.CreatedAt),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
