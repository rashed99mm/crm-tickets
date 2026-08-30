using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? ProfileImageUrl,
    Guid? DepartmentId,
    Guid? BranchId,
    Guid? TeamId
) : ICommand<Response<Guid>>;
