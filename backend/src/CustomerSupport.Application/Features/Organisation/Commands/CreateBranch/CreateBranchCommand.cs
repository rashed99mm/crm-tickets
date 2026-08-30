using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateBranch;

/// <summary>Records a new branch — AC-116, AC-123.</summary>
public record CreateBranchCommand(string Name, string? Region, string? Timezone) : ICommand<Response<Guid>>;
