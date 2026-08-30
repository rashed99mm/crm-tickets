using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Sla.Dtos;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Queries.GetSLAPolicies;

public class GetSLAPoliciesQueryHandler(
    IRepository<SLAPolicy> policies,
    IMessageFactory messages)
    : IQueryHandler<GetSLAPoliciesQuery, Response<PaginatedList<SLAPolicyDto>>>
{
    public async Task<Response<PaginatedList<SLAPolicyDto>>> Handle(GetSLAPoliciesQuery request, CancellationToken ct)
    {
        var page = await policies.GetPagedAsync(
            request,
            filter: null,
            p => new SLAPolicyDto(
                p.Id, p.Priority, p.ResponseTargetHours, p.ResolutionTargetHours,
                p.CategoryId, p.BranchId, p.IsActive, p.CreatedAt),
            ct);

        return messages.Success(page, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
