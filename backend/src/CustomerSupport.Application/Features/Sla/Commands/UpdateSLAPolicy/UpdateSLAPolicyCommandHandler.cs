using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.UpdateSLAPolicy;

public class UpdateSLAPolicyCommandHandler(
    IRepository<SLAPolicy> policies,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<UpdateSLAPolicyCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateSLAPolicyCommand request, CancellationToken ct)
    {
        var policy = await policies.GetTrackedAsync(request.Id, ct);
        if (policy is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.SLA.POLICY_NOT_FOUND);
        }

        policy.Update(
            request.Priority, request.ResponseTargetHours, request.ResolutionTargetHours,
            request.CategoryId, request.BranchId);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(policy.Id, ApplicationErrors.SLA.POLICY_UPDATED);
    }
}
