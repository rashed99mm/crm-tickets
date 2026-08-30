using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Sla.Commands.DeactivateSLAPolicy;

public class DeactivateSLAPolicyCommandHandler(
    IRepository<SLAPolicy> policies,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<DeactivateSLAPolicyCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeactivateSLAPolicyCommand request, CancellationToken ct)
    {
        var policy = await policies.GetTrackedAsync(request.Id, ct);
        if (policy is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.SLA.POLICY_NOT_FOUND);
        }

        policy.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.SLA.POLICY_DEACTIVATED);
    }
}
