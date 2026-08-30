using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Users.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;

namespace CustomerSupport.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler(
    IIdentityUserService identityUserService,
    IMessageFactory messages)
    : IQueryHandler<GetUserByIdQuery, Response<UserDto>>
{
    public async Task<Response<UserDto>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await identityUserService.FindByIdAsync(request.Id, ct);

        if (user is null)
        {
            return messages.NotFound<UserDto>(ApplicationErrors.User.NOT_FOUND);
        }

        var roles = await identityUserService.GetRolesAsync(user);

        var userDto = new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.EmailConfirmed,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt,
            roles);

        return messages.Success(userDto, ApplicationErrors.General.SUCCESS_OPERATION);
    }
}
