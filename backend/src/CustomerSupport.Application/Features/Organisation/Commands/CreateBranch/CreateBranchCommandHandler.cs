using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Domain.Interfaces;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateBranch;

public class CreateBranchCommandHandler(
    IRepository<Branch> branches,
    IUnitOfWork unitOfWork,
    IDbExceptionTranslator dbExceptionTranslator,
    IMessageFactory messages)
    : ICommandHandler<CreateBranchCommand, Response<Guid>>
{
    public async Task<Response<Guid>> Handle(CreateBranchCommand request, CancellationToken ct)
    {
        var branch = Branch.Create(request.Name, request.Region, request.Timezone);

        await branches.AddAsync(branch, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (dbExceptionTranslator.IsUniqueViolation(ex))
        {
            return messages.Fail<Guid>(ApplicationErrors.Branch.NAME_EXISTS, MessageType.Conflict);
        }

        return messages.Success(branch.Id, ApplicationErrors.Branch.CREATED);
    }
}
