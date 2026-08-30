using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Sla.Commands.DeactivateSLAPolicy;

/// <summary>Soft-deactivates an SLA policy — US-214.</summary>
public record DeactivateSLAPolicyCommand(Guid Id) : ICommand<Response<Unit>>;
