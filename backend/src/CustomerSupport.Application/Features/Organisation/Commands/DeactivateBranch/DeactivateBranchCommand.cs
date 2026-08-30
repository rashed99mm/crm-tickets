using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateBranch;

/// <summary>Soft-deactivates a branch — AC-123.</summary>
public record DeactivateBranchCommand(Guid Id) : ICommand<Response<Unit>>;
