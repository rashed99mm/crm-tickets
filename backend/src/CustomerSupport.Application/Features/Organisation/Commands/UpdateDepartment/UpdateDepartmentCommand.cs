using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.UpdateDepartment;

/// <summary>Corrects a department record — AC-119.</summary>
public record UpdateDepartmentCommand(Guid Id, string Name, Guid? ManagerId) : ICommand<Response<Guid>>;
