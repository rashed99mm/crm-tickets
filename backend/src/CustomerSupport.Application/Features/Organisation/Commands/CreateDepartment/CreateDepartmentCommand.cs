using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Organisation.Commands.CreateDepartment;

/// <summary>Records a new department — AC-115, AC-119.</summary>
public record CreateDepartmentCommand(string Name, Guid? ManagerId) : ICommand<Response<Guid>>;
