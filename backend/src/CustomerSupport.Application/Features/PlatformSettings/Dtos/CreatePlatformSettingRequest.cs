namespace CustomerSupport.Application.Features.PlatformSettings.Dtos;

public record CreatePlatformSettingRequest(
    string Key,
    string Value,
    string? Description,
    string Category,
    string ValueType,
    bool IsEncrypted,
    bool IsPublic
);
