using AutoMapper;
using CustomerSupport.Application.Features.PlatformSettings.Dtos;
using CustomerSupport.Domain.Entities.PlatformSettings;

namespace CustomerSupport.Infrastructure.Mapping;

public class PlatformSettingMappings : Profile
{
    public PlatformSettingMappings()
    {
        CreateMap<PlatformSetting, PlatformSettingDto>()
            .ForCtorParam(nameof(PlatformSettingDto.Value), opt => opt.MapFrom(src => src.IsEncrypted ? "***" : src.Value));
    }
}
