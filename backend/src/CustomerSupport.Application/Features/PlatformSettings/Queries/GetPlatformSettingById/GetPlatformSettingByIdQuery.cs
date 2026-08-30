using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingById;

public record GetPlatformSettingByIdQuery(Guid Id) : IQuery<Response<PlatformSettingDto>>;
