using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;

using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Domain.Entities.PlatformSettings;
using CustomerSupport.Domain.Interfaces;
using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Queries.GetPlatformSettingByKey;

public class GetPlatformSettingByKeyQueryHandler : IQueryHandler<GetPlatformSettingByKeyQuery, Response<PlatformSettingDto>>
{
    private readonly IRepository<PlatformSetting> _settingRepository;
    private readonly IMessageFactory _messages;

    public GetPlatformSettingByKeyQueryHandler(IRepository<PlatformSetting> settingRepository, IMessageFactory messages)
    {
        _settingRepository = settingRepository;
        _messages = messages;
    }

    public async Task<Response<PlatformSettingDto>> Handle(GetPlatformSettingByKeyQuery request, CancellationToken ct)
    {
        var setting = await _settingRepository.FirstOrDefaultAsync(
            s => s.Key == request.Key && (s.IsPublic || request.IsAdmin),
            ct);

        if (setting == null)
        {
            return _messages.Fail<PlatformSettingDto>(
                ApplicationErrors.PlatformSetting.NOT_FOUND,
                MessageType.NotFound);
        }

        return _messages.Success(MapToDto(setting), "PlatformSetting.Detail");
    }

    private static PlatformSettingDto MapToDto(PlatformSetting setting) => new(
        setting.Id,
        setting.Key,
        setting.IsEncrypted ? "***" : setting.Value,
        setting.Description,
        setting.Category,
        setting.ValueType,
        setting.IsEncrypted,
        setting.IsPublic,
        setting.CreatedAt
    );
}
