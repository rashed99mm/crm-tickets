using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateBranch;

/// <summary>Corrects a branch record — AC-123.</summary>
public record UpdateBranchCommand(Guid Id, string Name, string? Region, string? Timezone) : ICommand<Response<Guid>>;
