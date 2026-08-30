using CustomerSupport.Application.Contracts;

using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingByKey;

public record GetPlatformSettingByKeyQuery(string Key, bool IsAdmin) : IQuery<Response<PlatformSettingDto>>;
