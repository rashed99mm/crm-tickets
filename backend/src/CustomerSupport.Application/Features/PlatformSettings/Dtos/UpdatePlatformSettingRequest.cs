namespace CustomerSupport.Application.Features.PlatformSettings.Dtos;

public record UpdatePlatformSettingRequest(
    string? Value,
    string? Description,
    bool? IsEncrypted,
    bool? IsPublic
);
