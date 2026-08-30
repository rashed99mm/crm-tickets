using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Common;
using MediatR;

namespace CustomerSupport.Application.Features.Organisation.Commands.DeactivateDepartment;

/// <summary>Soft-deactivates a department — AC-119.</summary>
public record DeactivateDepartmentCommand(Guid Id) : ICommand<Response<Unit>>;
