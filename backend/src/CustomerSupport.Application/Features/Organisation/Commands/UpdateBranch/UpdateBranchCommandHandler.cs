using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateBranch;

public class UpdateBranchCommandHandler(
    IRepository<Branch> branches,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<UpdateBranchCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(UpdateBranchCommand request, CancellationToken ct)
    {
        var branch = await branches.GetTrackedAsync(request.Id, ct);
        if (branch is null)
        {
            return messages.NotFound<Guid>(ApplicationErrors.Branch.NOT_FOUND);
        }

        branch.Update(request.Name, request.Region, request.Timezone);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Branch.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(branch.Id, ApplicationErrors.Branch.UPDATED);
    }
}
