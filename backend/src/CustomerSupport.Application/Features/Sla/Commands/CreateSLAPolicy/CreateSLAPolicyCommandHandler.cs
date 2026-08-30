using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateSLAPolicy;

public class CreateSLAPolicyCommandHandler(
    IRepository<SLAPolicy> policies,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<CreateSLAPolicyCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateSLAPolicyCommand request, CancellationToken ct)
    {
        var policy = SLAPolicy.Create(
            request.Priority, request.ResponseTargetHours, request.ResolutionTargetHours,
            request.CategoryId, request.BranchId);

        await policies.AddAsync(policy, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(policy.Id, ApplicationErrors.SLA.POLICY_CREATED);
    }
}
