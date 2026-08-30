using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettings;

public class GetPlatformSettingsQueryHandler(IRepository<PlatformSetting> settingRepository, IMessageFactory messages) 
    : IQueryHandler<GetPlatformSettingsQuery, Response<PaginatedList<PlatformSettingDto>>>
{
    public async Task<Response<PaginatedList<PlatformSettingDto>>> Handle(GetPlatformSettingsQuery request, CancellationToken ct)
    {
        var filter = PredicateBuilder.True<PlatformSetting>()
            .WhereIf(!string.IsNullOrWhiteSpace(request.Category), s => s.Category == request.Category)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Key), s => s.Key.Contains(request.Key!))
            .WhereIf(!request.IncludePrivate, s => s.IsPublic);
            
        var result = await settingRepository.GetPagedAsync<PlatformSettingDto>(request, filter, ct);
        return messages.Success(result, "PlatformSetting.List");
    }
}
