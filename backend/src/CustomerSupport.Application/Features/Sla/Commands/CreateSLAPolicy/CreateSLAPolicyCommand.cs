using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Sla.Commands.CreateSLAPolicy;

/// <summary>Defines an SLA target per priority — AC-124.</summary>
public record CreateSLAPolicyCommand(
    string Priority, decimal ResponseTargetHours, decimal ResolutionTargetHours,
    Guid? CategoryId, Guid? BranchId) : ICommand<Response<Guid>>;

public record CreateSLAPolicyRequest(
    string Priority, decimal ResponseTargetHours, decimal ResolutionTargetHours,
    Guid? CategoryId, Guid? BranchId);
