using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Admin.Dtos;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Admin.Queries.GetAuditLog;

/// <summary>The audit trail, newest first — AC-140..AC-142.</summary>
public class GetAuditLogQuery : BasePagedQuery, IQuery<Response<PaginatedList<AuditLogDto>>>
{
    // BasePagedQuery has no default ordering (GetPagedAsync only sorts when SortBy is set) — these
    // overrides are what makes AC-140's "newest first" true rather than accidental row order.
    public GetAuditLogQuery()
    {
        SortBy = nameof(Domain.Entities.Audit.AuditLog.CreatedAt);
        SortDirection = "desc";
    }

    public string? ActionType { get; init; }
    public Guid? UserId { get; init; }
}
