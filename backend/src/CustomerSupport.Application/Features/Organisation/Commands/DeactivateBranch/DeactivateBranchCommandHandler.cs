using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateBranch;

public class DeactivateBranchCommandHandler(
    IRepository<Branch> branches,
    IUnitOfWork unitOfWork,
    IMessageFactory messages)
    : ICommandHandler<DeactivateBranchCommand, Response<Unit>>
{
    public async Task<Response<Unit>> Handle(DeactivateBranchCommand request, CancellationToken ct)
    {
        var branch = await branches.GetTrackedAsync(request.Id, ct);
        if (branch is null)
        {
            return messages.NotFound<Unit>(ApplicationErrors.Branch.NOT_FOUND);
        }

        branch.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);

        return messages.Success(Unit.Value, ApplicationErrors.Branch.DEACTIVATED);
    }
}
