using CustomerSupport.Application.Contracts;

using MediatR;

namespace CustomerSupport.Application.Features.PlatformSettings.Commands.CreatePlatformSetting;

public record CreatePlatformSettingCommand(
    string Key,
    string Value,
    string? Description,
    string Category,
    string ValueType,
    bool IsEncrypted,
    bool IsPublic
) : ICommand<Response<Guid>>;
