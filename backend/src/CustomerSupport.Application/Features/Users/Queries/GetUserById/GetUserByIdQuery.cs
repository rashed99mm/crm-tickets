using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Users.Dtos;

namespace CustomerSupport.Application.Features.Users.Queries.GetUserById;

/// <summary>
/// Retrieves a user by unique identifier.
/// </summary>
/// <param name="Id">The user identifier.</param>
public record GetUserByIdQuery(Guid Id) : IQuery<Response<UserDto>>;
