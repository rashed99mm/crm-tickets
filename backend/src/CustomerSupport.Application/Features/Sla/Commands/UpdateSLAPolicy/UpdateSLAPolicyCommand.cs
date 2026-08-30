using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Sla.Commands.UpdateSLAPolicy;

/// <summary>Corrects an SLA policy — US-214.</summary>
public record UpdateSLAPolicyCommand(
    Guid Id, string Priority, decimal ResponseTargetHours, decimal ResolutionTargetHours,
    Guid? CategoryId, Guid? BranchId) : ICommand<Response<Guid>>;
