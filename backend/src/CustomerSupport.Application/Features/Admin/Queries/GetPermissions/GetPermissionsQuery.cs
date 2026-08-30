using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Admin.Dtos;

namespace CustomerSupport.Application.Features.Admin.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IQuery<Response<PermissionAdministrationDto>>;
