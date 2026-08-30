using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.Auth.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IQuery<Response<UserInfoDto>>;
